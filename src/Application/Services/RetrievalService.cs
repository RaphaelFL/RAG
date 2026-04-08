using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;

namespace Chatbot.Application.Services;

public sealed class RetrievalService : IRetrievalService
{
    private readonly ISearchIndexGateway _indexGateway;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IApplicationCache _applicationCache;
    private readonly IRetrievalQueryPlanner _retrievalQueryPlanner;
    private readonly IRetrievalResultAuthorizer _retrievalResultAuthorizer;
    private readonly IRetrievalChunkSelector _retrievalChunkSelector;
    private readonly IRetrievalAuditLogger _retrievalAuditLogger;
    private readonly IRagRuntimeSettings _ragRuntimeSettings;
    private readonly ILogger<RetrievalService> _logger;

    public RetrievalService(
        ISearchIndexGateway indexGateway,
        IEmbeddingProvider embeddingProvider,
        IApplicationCache applicationCache,
        IRetrievalQueryPlanner retrievalQueryPlanner,
        IRetrievalResultAuthorizer retrievalResultAuthorizer,
        IRetrievalChunkSelector retrievalChunkSelector,
        IRetrievalAuditLogger retrievalAuditLogger,
        IRagRuntimeSettings ragRuntimeSettings,
        ILogger<RetrievalService> logger)
    {
        _indexGateway = indexGateway;
        _embeddingProvider = embeddingProvider;
        _applicationCache = applicationCache;
        _retrievalQueryPlanner = retrievalQueryPlanner;
        _retrievalResultAuthorizer = retrievalResultAuthorizer;
        _retrievalChunkSelector = retrievalChunkSelector;
        _retrievalAuditLogger = retrievalAuditLogger;
        _ragRuntimeSettings = ragRuntimeSettings;
        _logger = logger;
    }

    public async Task<RetrievalResultDto> RetrieveAsync(RetrievalQueryDto query, CancellationToken ct)
    {
        _logger.LogInformation("Retrieving documents for query: {query}", query.Query);
        using var activity = ChatbotTelemetry.ActivitySource.StartActivity("retrieval.query");
        activity?.SetTag("retrieval.top_k", query.TopK);

        var plan = _retrievalQueryPlanner.Create(query);

        var cached = await _applicationCache.GetAsync<RetrievalResultDto>(plan.CacheKey, ct);
        if (cached is not null)
        {
            await _retrievalAuditLogger.WriteAsync(new RetrievalAuditEntry
            {
                Query = query,
                Plan = plan,
                Result = cached,
                Diagnostics = new Dictionary<string, object?>
                {
                    ["candidateCount"] = plan.CandidateCount,
                    ["semanticRankingEnabled"] = plan.SemanticRankingEnabled,
                    ["cacheHit"] = true
                }
            }, ct);
            return cached;
        }

        var queryEmbedding = string.IsNullOrWhiteSpace(query.Query)
            ? null
            : await _embeddingProvider.CreateEmbeddingAsync(query.Query, null, ct);

        var startTime = DateTime.UtcNow;
        var results = await _indexGateway.HybridSearchAsync(query.Query, queryEmbedding, plan.CandidateCount, plan.Filters, ct);
        var elapsed = DateTime.UtcNow - startTime;
        ChatbotTelemetry.RetrievalLatencyMs.Record(elapsed.TotalMilliseconds);

        var authorizedResults = _retrievalResultAuthorizer.Authorize(results);

        var selectedChunks = _retrievalChunkSelector.Select(query.Query, authorizedResults, plan.Filters, plan.RequestedTopK);

        var retrievalResult = new RetrievalResultDto
        {
            Chunks = selectedChunks.ToList(),
            RetrievalStrategy = plan.SemanticRankingEnabled ? "hybrid-semantic-reranked" : "hybrid-reranked",
            LatencyMs = (long)elapsed.TotalMilliseconds
        };

        await _applicationCache.SetAsync(plan.CacheKey, retrievalResult, _ragRuntimeSettings.RetrievalCacheTtl, ct);
        await _retrievalAuditLogger.WriteAsync(new RetrievalAuditEntry
        {
            Query = query,
            Plan = plan,
            Result = retrievalResult,
            Diagnostics = new Dictionary<string, object?>
            {
                ["candidateCount"] = plan.CandidateCount,
                ["semanticRankingEnabled"] = plan.SemanticRankingEnabled,
                ["authorizedResults"] = authorizedResults.Count,
                ["rerankedResults"] = selectedChunks.Count,
                ["cacheHit"] = false
            }
        }, ct);
        return retrievalResult;
    }
}
