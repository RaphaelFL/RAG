using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Polly;

namespace Chatbot.Application.Services;

internal sealed class ChatTurnPreparer
{
    private readonly IRetrievalService _retrievalService;
    private readonly ICitationAssembler _citationAssembler;
    private readonly IChatCompletionProvider _chatCompletionProvider;
    private readonly IChatEvidenceSelector _chatEvidenceSelector;
    private readonly IChatCompletionCacheKeyFactory _chatCompletionCacheKeyFactory;
    private readonly IApplicationCache _applicationCache;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IRagRuntimeSettings _ragRuntimeSettings;
    private readonly ResiliencePipeline _resiliencePipeline;

    public ChatTurnPreparer(
        IRetrievalService retrievalService,
        ICitationAssembler citationAssembler,
        IChatCompletionProvider chatCompletionProvider,
        IChatEvidenceSelector chatEvidenceSelector,
        IChatCompletionCacheKeyFactory chatCompletionCacheKeyFactory,
        IApplicationCache applicationCache,
        IFeatureFlagService featureFlagService,
        IRagRuntimeSettings ragRuntimeSettings,
        ResiliencePipeline resiliencePipeline)
    {
        _retrievalService = retrievalService;
        _citationAssembler = citationAssembler;
        _chatCompletionProvider = chatCompletionProvider;
        _chatEvidenceSelector = chatEvidenceSelector;
        _chatCompletionCacheKeyFactory = chatCompletionCacheKeyFactory;
        _applicationCache = applicationCache;
        _featureFlagService = featureFlagService;
        _ragRuntimeSettings = ragRuntimeSettings;
        _resiliencePipeline = resiliencePipeline;
    }

    public async Task<PreparedChatTurn> PrepareAsync(
        ChatRequestDto request,
        PromptTemplateDefinition template,
        AgenticChatPlan plan,
        DateTime startTime,
        CancellationToken ct)
    {
        var maxContextChunks = Math.Min(_ragRuntimeSettings.MaxContextChunks, Math.Max(1, request.Options?.MaxCitations ?? 5));
        var shouldRetrieve = plan.RequiresRetrieval || HasExplicitRetrievalScope(request.Filters);
        var retrievalResult = shouldRetrieve
            ? await _resiliencePipeline.ExecuteAsync(async token => await _retrievalService.RetrieveAsync(new RetrievalQueryDto
            {
                Query = request.Message,
                TopK = maxContextChunks,
                DocumentIds = request.Filters?.DocumentIds,
                Tags = request.Filters?.Tags,
                Categories = request.Filters?.Categories,
                ContentTypes = request.Filters?.ContentTypes,
                Sources = request.Filters?.Sources,
                SemanticRanking = _featureFlagService.IsSemanticRankingEnabled && (request.Options?.SemanticRanking ?? true)
            }, token), ct)
            : new RetrievalResultDto
            {
                Chunks = new List<RetrievedChunkDto>(),
                RetrievalStrategy = "auto-llm",
                LatencyMs = 0
            };

        var evidenceChunks = _chatEvidenceSelector.Select(request.Message, retrievalResult.Chunks, maxContextChunks);
        var citations = _citationAssembler.Assemble(evidenceChunks, request.Options?.MaxCitations ?? 5);
        var canUseGeneralKnowledge = plan.AllowsGeneralKnowledge;

        ChatCompletionResult? completionResult = null;
        var responseMessage = template.InsufficientEvidenceMessage;
        if (evidenceChunks.Count > 0 || canUseGeneralKnowledge)
        {
            var effectivePlan = new AgenticChatPlan
            {
                RequiresRetrieval = evidenceChunks.Count > 0,
                AllowsGeneralKnowledge = canUseGeneralKnowledge,
                PreferStreaming = plan.PreferStreaming,
                ExecutionMode = ResolveExecutionMode(evidenceChunks.Count > 0, canUseGeneralKnowledge)
            };

            var completionCacheKey = _chatCompletionCacheKeyFactory.Build(request, template, effectivePlan, evidenceChunks);
            completionResult = await _applicationCache.GetAsync<ChatCompletionResult>(completionCacheKey, ct);

            if (completionResult is null)
            {
                completionResult = await _resiliencePipeline.ExecuteAsync(async token => await _chatCompletionProvider.CompleteAsync(new ChatCompletionRequest
                {
                    Message = request.Message,
                    Template = template,
                    AllowGeneralKnowledge = canUseGeneralKnowledge,
                    RetrievedChunks = evidenceChunks
                }, token), ct);

                if (!string.IsNullOrWhiteSpace(completionResult.Message))
                {
                    await _applicationCache.SetAsync(completionCacheKey, completionResult, _ragRuntimeSettings.ChatCompletionCacheTtl, ct);
                }
            }

            responseMessage = string.IsNullOrWhiteSpace(completionResult.Message)
                ? template.InsufficientEvidenceMessage
                : completionResult.Message;
        }

        var elapsed = DateTime.UtcNow - startTime;
        return new PreparedChatTurn
        {
            ResponseMessage = responseMessage,
            Citations = citations,
            Usage = BuildUsage(completionResult, elapsed, shouldRetrieve, retrievalResult, evidenceChunks.Count, canUseGeneralKnowledge, maxContextChunks, citations.Count),
            Policy = new ChatPolicyDto
            {
                Grounded = evidenceChunks.Count > 0,
                HadEnoughEvidence = evidenceChunks.Count > 0,
                TemplateId = template.TemplateId,
                TemplateVersion = template.Version
            }
        };
    }

    private static UsageMetadataDto BuildUsage(
        ChatCompletionResult? completionResult,
        TimeSpan elapsed,
        bool attemptedRetrieval,
        RetrievalResultDto retrievalResult,
        int evidenceChunkCount,
        bool allowsGeneralKnowledge,
        int maxContextChunks,
        int citationCount)
    {
        return new UsageMetadataDto
        {
            Model = completionResult?.Model ?? "policy-only",
            PromptTokens = completionResult?.PromptTokens ?? 0,
            CompletionTokens = completionResult?.CompletionTokens ?? 0,
            TotalTokens = completionResult?.TotalTokens ?? 0,
            LatencyMs = (long)elapsed.TotalMilliseconds,
            RetrievalStrategy = ResolveRetrievalStrategy(attemptedRetrieval, retrievalResult.RetrievalStrategy, evidenceChunkCount, allowsGeneralKnowledge),
            RuntimeMetrics = new Dictionary<string, long>
            {
                ["max_context_chunks"] = maxContextChunks,
                ["retrieved_chunks"] = retrievalResult.Chunks.Count,
                ["evidence_chunks"] = evidenceChunkCount,
                ["citations"] = citationCount
            }
        };
    }

    private static string ResolveExecutionMode(bool hasEvidence, bool allowsGeneralKnowledge)
    {
        if (hasEvidence)
        {
            return allowsGeneralKnowledge ? "auto-hybrid" : "auto-rag";
        }

        return allowsGeneralKnowledge ? "auto-llm" : "grounded";
    }

    private static string ResolveRetrievalStrategy(bool attemptedRetrieval, string retrievalStrategy, int evidenceChunkCount, bool allowsGeneralKnowledge)
    {
        if (evidenceChunkCount > 0)
        {
            return allowsGeneralKnowledge ? $"auto-hybrid:{retrievalStrategy}" : $"auto-rag:{retrievalStrategy}";
        }

        if (attemptedRetrieval)
        {
            return allowsGeneralKnowledge ? "auto-llm:fallback-after-retrieval" : retrievalStrategy;
        }

        return allowsGeneralKnowledge ? "auto-llm" : "grounded";
    }

    private static bool HasExplicitRetrievalScope(ChatFiltersDto? filters)
    {
        if (filters is null)
        {
            return false;
        }

        return filters.DocumentIds is { Count: > 0 }
            || filters.Tags is { Count: > 0 }
            || filters.Categories is { Count: > 0 }
            || filters.ContentTypes is { Count: > 0 }
            || filters.Sources is { Count: > 0 };
    }
}