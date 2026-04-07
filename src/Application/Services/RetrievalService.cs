using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chatbot.Domain.Entities;

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
    private readonly IOperationalAuditStore _operationalAuditStore;
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
        IOperationalAuditStore operationalAuditStore,
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
        _operationalAuditStore = operationalAuditStore;
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
        var cacheKey = BuildRetrievalCacheKey(query.Query, requestedTopK, candidateCount, semanticRankingEnabled, filters);

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

        var rerankedResults = authorizedResults
            .Select(result => new
            {
                Result = result,
                Score = ComputeRerankedScore(query.Query, result, filters)
            })
            .Where(item => item.Score >= _ragRuntimeSettings.MinimumRerankScore)
            .OrderByDescending(item => item.Score)
            .Take(requestedTopK)
            .ToList();

        if (rerankedResults.Count == 0)
        {
            rerankedResults = authorizedResults
                .OrderByDescending(result => result.Score)
                .Take(requestedTopK)
                .Select(result => new { Result = result, Score = result.Score })
                .ToList();
        }

        var retrievalResult = new RetrievalResultDto
        {
            Chunks = rerankedResults
                .Select(item => new RetrievedChunkDto
                {
                    ChunkId = item.Result.ChunkId,
                    DocumentId = item.Result.DocumentId,
                    Content = item.Result.Content,
                    Score = item.Score,
                    DocumentTitle = item.Result.Metadata.ContainsKey("title") ? item.Result.Metadata["title"] : "Unknown",
                    PageNumber = ParseMetadataInt(item.Result.Metadata, "startPage"),
                    EndPageNumber = ParseMetadataInt(item.Result.Metadata, "endPage"),
                    Section = item.Result.Metadata.TryGetValue("section", out var sectionValue) ? sectionValue : string.Empty
                })
                .ToList(),
            RetrievalStrategy = semanticRankingEnabled ? "hybrid-semantic-reranked" : "hybrid-reranked",
            LatencyMs = (long)elapsed.TotalMilliseconds
        };

        await _applicationCache.SetAsync(cacheKey, retrievalResult, _ragRuntimeSettings.RetrievalCacheTtl, ct);
        await WriteRetrievalLogAsync(query, requestedTopK, retrievalResult, filters, new Dictionary<string, object?>
        {
            ["candidateCount"] = candidateCount,
            ["semanticRankingEnabled"] = semanticRankingEnabled,
            ["authorizedResults"] = authorizedResults.Count,
            ["rerankedResults"] = rerankedResults.Count,
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
        return _operationalAuditStore.WriteRetrievalLogAsync(new RetrievalLogRecord
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

    public async Task<SearchQueryResponseDto> QueryAsync(SearchQueryRequestDto query, CancellationToken ct)
    {
        var retrievalResult = await RetrieveAsync(new RetrievalQueryDto
        {
            Query = query.Query,
            TopK = query.Top,
            DocumentIds = query.Filters?.DocumentIds,
            Tags = query.Filters?.Tags,
            Categories = query.Filters?.Categories,
            ContentTypes = query.Filters?.ContentTypes,
            Sources = query.Filters?.Sources,
            SemanticRanking = query.SemanticRanking
        }, ct);

        return new SearchQueryResponseDto
        {
            Items = retrievalResult.Chunks.Select(chunk => new SearchQueryItemDto
            {
                DocumentId = chunk.DocumentId,
                ChunkId = chunk.ChunkId,
                Title = chunk.DocumentTitle,
                Snippet = chunk.Content[..Math.Min(200, chunk.Content.Length)],
                Score = chunk.Score
            }).ToList(),
            Count = retrievalResult.Chunks.Count
        };
    }

    private string BuildRetrievalCacheKey(
        string query,
        int requestedTopK,
        int candidateCount,
        bool semanticRankingEnabled,
        FileSearchFilterDto filters)
    {
        var tenantId = _requestContextAccessor.TenantId?.ToString() ?? string.Empty;
        var userId = _requestContextAccessor.UserId ?? string.Empty;
        var userRole = _requestContextAccessor.UserRole ?? string.Empty;
        var documentIds = filters.DocumentIds is { Count: > 0 }
            ? string.Join(',', filters.DocumentIds.OrderBy(id => id))
            : string.Empty;
        var tags = filters.Tags is { Count: > 0 }
            ? string.Join(',', filters.Tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        var categories = filters.Categories is { Count: > 0 }
            ? string.Join(',', filters.Categories.OrderBy(category => category, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        var contentTypes = filters.ContentTypes is { Count: > 0 }
            ? string.Join(',', filters.ContentTypes.OrderBy(contentType => contentType, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        var sources = filters.Sources is { Count: > 0 }
            ? string.Join(',', filters.Sources.OrderBy(source => source, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        var coherencyStamp = ResolveCacheCoherencyStamp(filters);

        return $"retrieval:{ComputeHash(string.Join("||", new[]
        {
            query.Trim(),
            requestedTopK.ToString(),
            candidateCount.ToString(),
            semanticRankingEnabled.ToString(),
            tenantId,
            userId,
            userRole,
            documentIds,
            tags,
            categories,
            contentTypes,
            sources,
            coherencyStamp
        }))}";
    }

    private string ResolveCacheCoherencyStamp(FileSearchFilterDto filters)
    {
        var scopedDocuments = _documentCatalog.Query(new FileSearchFilterDto
        {
            TenantId = filters.TenantId,
            DocumentIds = filters.DocumentIds,
            Tags = filters.Tags,
            Categories = filters.Categories,
            ContentTypes = filters.ContentTypes,
            Sources = filters.Sources
        });

        if (scopedDocuments.Count == 0)
        {
            return "empty-scope";
        }

        return ComputeHash(string.Join('|', scopedDocuments
            .OrderBy(document => document.DocumentId)
            .Select(document => $"{document.DocumentId:N}:{document.Version}:{document.UpdatedAtUtc.Ticks}")));
    }

    private double ComputeRerankedScore(string query, SearchResultDto result, FileSearchFilterDto filters)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (terms.Length == 0)
        {
            return result.Score;
        }

        var title = result.Metadata.TryGetValue("title", out var titleValue) ? titleValue : string.Empty;
        var tags = GetMetadataValues(result.Metadata, "tags");
        var categories = GetMetadataValues(result.Metadata, "categories");
        var category = result.Metadata.TryGetValue("category", out var categoryValue) ? categoryValue : string.Empty;
        var contentType = result.Metadata.TryGetValue("contentType", out var contentTypeValue) ? contentTypeValue : string.Empty;
        var source = result.Metadata.TryGetValue("source", out var sourceValue) ? sourceValue : string.Empty;
        var exactMatches = terms.Count(term => result.Content.Contains(term, StringComparison.OrdinalIgnoreCase));
        var titleMatches = terms.Any(term => title.Contains(term, StringComparison.OrdinalIgnoreCase));
        var filterMatches = 0;

        if (filters.Tags is { Count: > 0 } && filters.Tags.Any(tag => tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
        {
            filterMatches++;
        }

        if (filters.Categories is { Count: > 0 }
            && (filters.Categories.Any(filter => categories.Contains(filter, StringComparer.OrdinalIgnoreCase))
                || filters.Categories.Any(filter => string.Equals(filter, category, StringComparison.OrdinalIgnoreCase))))
        {
            filterMatches++;
        }

        if (filters.ContentTypes is { Count: > 0 }
            && filters.ContentTypes.Any(filter => string.Equals(filter, contentType, StringComparison.OrdinalIgnoreCase)))
        {
            filterMatches++;
        }

        if (filters.Sources is { Count: > 0 }
            && filters.Sources.Any(filter => string.Equals(filter, source, StringComparison.OrdinalIgnoreCase)))
        {
            filterMatches++;
        }

        var score = result.Score + (exactMatches / (double)terms.Length) * _ragRuntimeSettings.ExactMatchBoost;
        if (titleMatches)
        {
            score += _ragRuntimeSettings.TitleMatchBoost;
        }

        if (filterMatches > 0)
        {
            score += filterMatches * _ragRuntimeSettings.FilterMatchBoost;
        }

        return Math.Round(score, 4);
    }

    private static string[] GetMetadataValues(Dictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var rawValue)
            ? rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();
    }

    private static int ParseMetadataInt(Dictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, out var parsed)
            ? parsed
            : 1;
    }

    private static string ComputeHash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
