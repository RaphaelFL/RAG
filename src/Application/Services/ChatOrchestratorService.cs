using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Chatbot.Application.Services;

public class ChatOrchestratorService : IChatOrchestrator
{
    private readonly IAgenticChatPlanner _agenticChatPlanner;
    private readonly IChatRequestTemplateResolver _chatRequestTemplateResolver;
    private readonly IChatStreamingSegmenter _chatStreamingSegmenter;
    private readonly IChatTurnPreparer _chatTurnPreparer;
    private readonly IChatTurnPersister _chatTurnPersister;
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
        : this(
            agenticChatPlanner,
            chatRequestTemplateResolver,
            chatStreamingSegmenter,
            new ChatTurnPreparer(
                new ChatRetrievalContextBuilder(
                    retrievalService,
                    citationAssembler,
                    chatEvidenceSelector,
                    featureFlagService,
                    ragRuntimeSettings,
                    resiliencePipeline),
                new ChatCompletionResponseBuilder(
                    chatCompletionProvider,
                    chatCompletionCacheKeyFactory,
                    applicationCache,
                    ragRuntimeSettings,
                    resiliencePipeline),
                new PreparedChatTurnFactory()),
            new ChatTurnPersister(chatSessionStore, requestContextAccessor),
            logger)
    {
    }

    [ActivatorUtilitiesConstructor]
    public ChatOrchestratorService(
        IAgenticChatPlanner agenticChatPlanner,
        IChatRequestTemplateResolver chatRequestTemplateResolver,
        IChatStreamingSegmenter chatStreamingSegmenter,
        IChatTurnPreparer chatTurnPreparer,
        IChatTurnPersister chatTurnPersister,
        ILogger<ChatOrchestratorService> logger)
    {
        _agenticChatPlanner = agenticChatPlanner;
        _chatRequestTemplateResolver = chatRequestTemplateResolver;
        _chatStreamingSegmenter = chatStreamingSegmenter;
        _chatTurnPreparer = chatTurnPreparer;
        _chatTurnPersister = chatTurnPersister;
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
            var preparedTurn = await _chatTurnPreparer.PrepareAsync(request, template, plan, startTime, ct);

            await _chatTurnPersister.PersistAsync(request, answerId, template, preparedTurn, ct);

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
        var preparedTurn = await _chatTurnPreparer.PrepareAsync(request, template, plan, startTime, ct);

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

        await _chatTurnPersister.PersistAsync(request, answerId, template, preparedTurn, ct);

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
}
