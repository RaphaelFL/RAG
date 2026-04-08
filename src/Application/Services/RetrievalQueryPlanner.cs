using Chatbot.Application.Abstractions;

namespace Chatbot.Application.Services;

public sealed class RetrievalQueryPlanner : IRetrievalQueryPlanner
{
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IRagRuntimeSettings _ragRuntimeSettings;
    private readonly IRetrievalCacheKeyFactory _retrievalCacheKeyFactory;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly IDocumentCatalog _documentCatalog;

    public RetrievalQueryPlanner(
        IFeatureFlagService featureFlagService,
        IRagRuntimeSettings ragRuntimeSettings,
        IRetrievalCacheKeyFactory retrievalCacheKeyFactory,
        IRequestContextAccessor requestContextAccessor,
        IDocumentCatalog documentCatalog)
    {
        _featureFlagService = featureFlagService;
        _ragRuntimeSettings = ragRuntimeSettings;
        _retrievalCacheKeyFactory = retrievalCacheKeyFactory;
        _requestContextAccessor = requestContextAccessor;
        _documentCatalog = documentCatalog;
    }

    public RetrievalExecutionPlan Create(RetrievalQueryDto query)
    {
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
        var cacheKey = _retrievalCacheKeyFactory.Build(
            query.Query,
            requestedTopK,
            candidateCount,
            semanticRankingEnabled,
            filters,
            _requestContextAccessor,
            _documentCatalog);

        return new RetrievalExecutionPlan
        {
            Filters = filters,
            RequestedTopK = requestedTopK,
            CandidateCount = candidateCount,
            SemanticRankingEnabled = semanticRankingEnabled,
            CacheKey = cacheKey
        };
    }
}