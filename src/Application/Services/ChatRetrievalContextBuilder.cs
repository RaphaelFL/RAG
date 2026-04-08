using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Polly;

namespace Chatbot.Application.Services;

internal sealed class ChatRetrievalContextBuilder
{
    private readonly IRetrievalService _retrievalService;
    private readonly ICitationAssembler _citationAssembler;
    private readonly IChatEvidenceSelector _chatEvidenceSelector;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IRagRuntimeSettings _ragRuntimeSettings;
    private readonly ResiliencePipeline _resiliencePipeline;

    public ChatRetrievalContextBuilder(
        IRetrievalService retrievalService,
        ICitationAssembler citationAssembler,
        IChatEvidenceSelector chatEvidenceSelector,
        IFeatureFlagService featureFlagService,
        IRagRuntimeSettings ragRuntimeSettings,
        ResiliencePipeline resiliencePipeline)
    {
        _retrievalService = retrievalService;
        _citationAssembler = citationAssembler;
        _chatEvidenceSelector = chatEvidenceSelector;
        _featureFlagService = featureFlagService;
        _ragRuntimeSettings = ragRuntimeSettings;
        _resiliencePipeline = resiliencePipeline;
    }

    public async Task<ChatRetrievalContext> BuildAsync(ChatRequestDto request, AgenticChatPlan plan, CancellationToken ct)
    {
        var maxContextChunks = Math.Min(_ragRuntimeSettings.MaxContextChunks, Math.Max(1, request.Options?.MaxCitations ?? 5));
        var attemptedRetrieval = plan.RequiresRetrieval || HasExplicitRetrievalScope(request.Filters);
        var retrievalResult = attemptedRetrieval
            ? await _resiliencePipeline.ExecuteAsync(async token => await _retrievalService.RetrieveAsync(CreateRetrievalQuery(request, maxContextChunks), token), ct)
            : new RetrievalResultDto
            {
                Chunks = new List<RetrievedChunkDto>(),
                RetrievalStrategy = "auto-llm",
                LatencyMs = 0
            };

        var evidenceChunks = _chatEvidenceSelector.Select(request.Message, retrievalResult.Chunks, maxContextChunks);
        var citations = _citationAssembler.Assemble(evidenceChunks, request.Options?.MaxCitations ?? 5);

        return new ChatRetrievalContext
        {
            RetrievalResult = retrievalResult,
            EvidenceChunks = evidenceChunks,
            Citations = citations,
            MaxContextChunks = maxContextChunks,
            AttemptedRetrieval = attemptedRetrieval,
            AllowsGeneralKnowledge = plan.AllowsGeneralKnowledge
        };
    }

    private RetrievalQueryDto CreateRetrievalQuery(ChatRequestDto request, int maxContextChunks)
    {
        return new RetrievalQueryDto
        {
            Query = request.Message,
            TopK = maxContextChunks,
            DocumentIds = request.Filters?.DocumentIds,
            Tags = request.Filters?.Tags,
            Categories = request.Filters?.Categories,
            ContentTypes = request.Filters?.ContentTypes,
            Sources = request.Filters?.Sources,
            SemanticRanking = _featureFlagService.IsSemanticRankingEnabled && (request.Options?.SemanticRanking ?? true)
        };
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