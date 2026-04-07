using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using System.Text.Json;

namespace Chatbot.Application.Services;

public class RetrievalService : IRetrievalService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ISearchIndexGateway _indexGateway;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IDocumentAuthorizationService _documentAuthorizationService;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly IApplicationCache _applicationCache;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IRagRuntimeSettings _ragRuntimeSettings;
    private readonly IRetrievalCacheKeyFactory _retrievalCacheKeyFactory;
    private readonly IRetrievalChunkSelector _retrievalChunkSelector;
    private readonly IOperationalAuditWriter _operationalAuditWriter;
    private readonly ILogger<RetrievalService> _logger;

    public RetrievalService(
        ISearchIndexGateway indexGateway,
        IEmbeddingProvider embeddingProvider,
        IDocumentCatalog documentCatalog,
        IDocumentAuthorizationService documentAuthorizationService,
        IRequestContextAccessor requestContextAccessor,
        IApplicationCache applicationCache,
        IFeatureFlagService featureFlagService,
        IRagRuntimeSettings ragRuntimeSettings,
        IRetrievalCacheKeyFactory retrievalCacheKeyFactory,
        IRetrievalChunkSelector retrievalChunkSelector,
        IOperationalAuditWriter operationalAuditWriter,
        ILogger<RetrievalService> logger)
    {
        _indexGateway = indexGateway;
        _embeddingProvider = embeddingProvider;
        _documentCatalog = documentCatalog;
        _documentAuthorizationService = documentAuthorizationService;
        _requestContextAccessor = requestContextAccessor;
        _applicationCache = applicationCache;
        _featureFlagService = featureFlagService;
        _ragRuntimeSettings = ragRuntimeSettings;
        _retrievalCacheKeyFactory = retrievalCacheKeyFactory;
        _retrievalChunkSelector = retrievalChunkSelector;
        _operationalAuditWriter = operationalAuditWriter;
        _logger = logger;
    }

    public async Task<RetrievalResultDto> RetrieveAsync(RetrievalQueryDto query, CancellationToken ct)
    {
        _logger.LogInformation("Retrieving documents for query: {query}", query.Query);
        using var activity = ChatbotTelemetry.ActivitySource.StartActivity("retrieval.query");
        activity?.SetTag("retrieval.top_k", query.TopK);

        var filters = new FileSearchFilterDto
        {
            DocumentIds = query.DocumentIds,
            Tags = query.Tags,
            Categories = query.Categories,
            ContentTypes = query.ContentTypes,
            Sources = query.Sources,
            TenantId = _requestContextAccessor.TenantId
        };
        var requestedTopK = Math.Max(1, query.TopK);
        var candidateCount = Math.Min(
            _ragRuntimeSettings.RetrievalMaxCandidateCount,
            Math.Max(requestedTopK, requestedTopK * _ragRuntimeSettings.RetrievalCandidateMultiplier));
        var semanticRankingEnabled = _featureFlagService.IsSemanticRankingEnabled && query.SemanticRanking;
        var cacheKey = _retrievalCacheKeyFactory.Build(query.Query, requestedTopK, candidateCount, semanticRankingEnabled, filters, _requestContextAccessor, _documentCatalog);

        var cached = await _applicationCache.GetAsync<RetrievalResultDto>(cacheKey, ct);
        if (cached is not null)
        {
            await WriteRetrievalLogAsync(query, requestedTopK, cached, filters, new Dictionary<string, object?>
            {
                ["candidateCount"] = candidateCount,
                ["semanticRankingEnabled"] = semanticRankingEnabled,
                ["cacheHit"] = true
            }, ct);
            return cached;
        }

        var queryEmbedding = string.IsNullOrWhiteSpace(query.Query)
            ? null
            : await _embeddingProvider.CreateEmbeddingAsync(query.Query, null, ct);

        var startTime = DateTime.UtcNow;
        var results = await _indexGateway.HybridSearchAsync(query.Query, queryEmbedding, candidateCount, filters, ct);
        var elapsed = DateTime.UtcNow - startTime;
        ChatbotTelemetry.RetrievalLatencyMs.Record(elapsed.TotalMilliseconds);

        var authorizedResults = results
            .Where(result =>
            {
                var document = _documentCatalog.Get(result.DocumentId);
                return document is not null && _documentAuthorizationService.CanAccess(
                    document,
                    _requestContextAccessor.TenantId,
                    _requestContextAccessor.UserId,
                    _requestContextAccessor.UserRole);
            })
            .ToList();

        var selectedChunks = _retrievalChunkSelector.Select(query.Query, authorizedResults, filters, requestedTopK);

        var retrievalResult = new RetrievalResultDto
        {
            Chunks = selectedChunks.ToList(),
            RetrievalStrategy = semanticRankingEnabled ? "hybrid-semantic-reranked" : "hybrid-reranked",
            LatencyMs = (long)elapsed.TotalMilliseconds
        };

        await _applicationCache.SetAsync(cacheKey, retrievalResult, _ragRuntimeSettings.RetrievalCacheTtl, ct);
        await WriteRetrievalLogAsync(query, requestedTopK, retrievalResult, filters, new Dictionary<string, object?>
        {
            ["candidateCount"] = candidateCount,
            ["semanticRankingEnabled"] = semanticRankingEnabled,
            ["authorizedResults"] = authorizedResults.Count,
            ["rerankedResults"] = selectedChunks.Count,
            ["cacheHit"] = false
        }, ct);
        return retrievalResult;
    }

    private Task WriteRetrievalLogAsync(
        RetrievalQueryDto query,
        int requestedTopK,
        RetrievalResultDto retrievalResult,
        FileSearchFilterDto filters,
        Dictionary<string, object?> diagnostics,
        CancellationToken ct)
    {
        return _operationalAuditWriter.WriteRetrievalLogAsync(new RetrievalLogRecord
        {
            RetrievalLogId = Guid.NewGuid(),
            TenantId = _requestContextAccessor.TenantId ?? Guid.Empty,
            QueryText = query.Query,
            Strategy = retrievalResult.RetrievalStrategy,
            RequestedTopK = requestedTopK,
            ReturnedTopK = retrievalResult.Chunks.Count,
            FiltersJson = JsonSerializer.Serialize(filters, SerializerOptions),
            DiagnosticsJson = JsonSerializer.Serialize(diagnostics, SerializerOptions),
            CreatedAtUtc = DateTime.UtcNow
        }, ct);
    }

}
