using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Polly;

namespace Chatbot.Application.Services;

public class ChatOrchestratorService : IChatOrchestrator
{
    private readonly IRetrievalService _retrievalService;
    private readonly ICitationAssembler _citationAssembler;
    private readonly IAgenticChatPlanner _agenticChatPlanner;
    private readonly IChatCompletionProvider _chatCompletionProvider;
    private readonly IChatRequestTemplateResolver _chatRequestTemplateResolver;
    private readonly IChatEvidenceSelector _chatEvidenceSelector;
    private readonly IChatStreamingSegmenter _chatStreamingSegmenter;
    private readonly IChatCompletionCacheKeyFactory _chatCompletionCacheKeyFactory;
    private readonly IChatSessionStore _chatSessionStore;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly IApplicationCache _applicationCache;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IRagRuntimeSettings _ragRuntimeSettings;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<ChatOrchestratorService> _logger;

    public ChatOrchestratorService(
        IRetrievalService retrievalService,
        ICitationAssembler citationAssembler,
        IAgenticChatPlanner agenticChatPlanner,
        IChatCompletionProvider chatCompletionProvider,
        IChatRequestTemplateResolver chatRequestTemplateResolver,
        IChatEvidenceSelector chatEvidenceSelector,
        IChatStreamingSegmenter chatStreamingSegmenter,
        IChatCompletionCacheKeyFactory chatCompletionCacheKeyFactory,
        IChatSessionStore chatSessionStore,
        IRequestContextAccessor requestContextAccessor,
        IApplicationCache applicationCache,
        IFeatureFlagService featureFlagService,
        IRagRuntimeSettings ragRuntimeSettings,
        ResiliencePipeline resiliencePipeline,
        ILogger<ChatOrchestratorService> logger)
    {
        _retrievalService = retrievalService;
        _citationAssembler = citationAssembler;
        _agenticChatPlanner = agenticChatPlanner;
        _chatCompletionProvider = chatCompletionProvider;
        _chatRequestTemplateResolver = chatRequestTemplateResolver;
        _chatEvidenceSelector = chatEvidenceSelector;
        _chatStreamingSegmenter = chatStreamingSegmenter;
        _chatCompletionCacheKeyFactory = chatCompletionCacheKeyFactory;
        _chatSessionStore = chatSessionStore;
        _requestContextAccessor = requestContextAccessor;
        _applicationCache = applicationCache;
        _featureFlagService = featureFlagService;
        _ragRuntimeSettings = ragRuntimeSettings;
        _resiliencePipeline = resiliencePipeline;
        _logger = logger;
    }

    public async Task<ChatResponseDto> SendAsync(ChatRequestDto request, CancellationToken ct)
    {
        _logger.LogInformation("Processing chat request for session {sessionId}", request.SessionId);

        var answerId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;
        var template = _chatRequestTemplateResolver.Resolve(request);

        try
        {
            using var activity = ChatbotTelemetry.ActivitySource.StartActivity("chat.send");
            activity?.SetTag("chat.session_id", request.SessionId);
            activity?.SetTag("chat.template_id", request.TemplateId);
            var plan = _agenticChatPlanner.CreatePlan(request);
            activity?.SetTag("chat.execution_mode", plan.ExecutionMode);
            var preparedTurn = await PrepareTurnAsync(request, template, plan, startTime, ct);

            await PersistTurnAsync(request, answerId, template, preparedTurn, ct);

            return new ChatResponseDto
            {
                AnswerId = answerId,
                SessionId = request.SessionId,
                Message = preparedTurn.ResponseMessage,
                Citations = preparedTurn.Citations,
                Usage = preparedTurn.Usage,
                Policy = preparedTurn.Policy,
                TimestampUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            throw;
        }
    }

    public async IAsyncEnumerable<StreamingChatEventDto> StreamAsync(ChatRequestDto request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var answerId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;

        // Send started event
        yield return new StreamingChatEventDto
        {
            EventType = "started",
            Data = new StreamStartedEventDto { AnswerId = answerId, SessionId = request.SessionId }
        };

        var template = _chatRequestTemplateResolver.Resolve(request);
        var plan = _agenticChatPlanner.CreatePlan(request);
        using var activity = ChatbotTelemetry.ActivitySource.StartActivity("chat.stream");
        activity?.SetTag("chat.session_id", request.SessionId);
        activity?.SetTag("chat.execution_mode", plan.ExecutionMode);
        var preparedTurn = await PrepareTurnAsync(request, template, plan, startTime, ct);

        foreach (var segment in _chatStreamingSegmenter.Segment(preparedTurn.ResponseMessage))
        {
            yield return new StreamingChatEventDto
            {
                EventType = "delta",
                Data = new StreamDeltaEventDto { Text = segment }
            };
        }

        // Send citations
        foreach (var citation in preparedTurn.Citations)
        {
            yield return new StreamingChatEventDto
            {
                EventType = "citation",
                Data = citation
            };
        }

        await PersistTurnAsync(request, answerId, template, preparedTurn, ct);

        // Send completed event
        yield return new StreamingChatEventDto
        {
            EventType = "completed",
            Data = new StreamCompletedEventDto
            {
                Usage = preparedTurn.Usage
            }
        };
    }

    private async Task<PreparedChatTurn> PrepareTurnAsync(
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
        var usage = new UsageMetadataDto
        {
            Model = completionResult?.Model ?? "policy-only",
            PromptTokens = completionResult?.PromptTokens ?? 0,
            CompletionTokens = completionResult?.CompletionTokens ?? 0,
            TotalTokens = completionResult?.TotalTokens ?? 0,
            LatencyMs = (long)elapsed.TotalMilliseconds,
            RetrievalStrategy = ResolveRetrievalStrategy(shouldRetrieve, retrievalResult.RetrievalStrategy, evidenceChunks.Count, canUseGeneralKnowledge),
            RuntimeMetrics = new Dictionary<string, long>
            {
                ["max_context_chunks"] = maxContextChunks,
                ["retrieved_chunks"] = retrievalResult.Chunks.Count,
                ["evidence_chunks"] = evidenceChunks.Count,
                ["citations"] = citations.Count
            }
        };

        return new PreparedChatTurn
        {
            ResponseMessage = responseMessage,
            Citations = citations,
            Usage = usage,
            Policy = new ChatPolicyDto
            {
                Grounded = evidenceChunks.Count > 0,
                HadEnoughEvidence = evidenceChunks.Count > 0,
                TemplateId = template.TemplateId,
                TemplateVersion = template.Version
            }
        };
    }

    private async Task PersistTurnAsync(
        ChatRequestDto request,
        Guid answerId,
        PromptTemplateDefinition template,
        PreparedChatTurn preparedTurn,
        CancellationToken ct)
    {
        await _chatSessionStore.AppendTurnAsync(new ChatSessionTurnRecord
        {
            SessionId = request.SessionId,
            TenantId = _requestContextAccessor.TenantId ?? Guid.Empty,
            UserId = ParseUserId(_requestContextAccessor.UserId),
            AnswerId = answerId,
            UserMessage = request.Message,
            AssistantMessage = preparedTurn.ResponseMessage,
            Citations = preparedTurn.Citations,
            Usage = preparedTurn.Usage,
            TemplateId = template.TemplateId,
            TemplateVersion = template.Version,
            TimestampUtc = DateTime.UtcNow
        }, ct);
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

    private static Guid ParseUserId(string? rawUserId)
    {
        return Guid.TryParse(rawUserId, out var parsedUserId)
            ? parsedUserId
            : Guid.Empty;
    }
}
