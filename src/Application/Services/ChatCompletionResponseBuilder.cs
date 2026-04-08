using Chatbot.Application.Abstractions;
using Polly;

namespace Chatbot.Application.Services;

internal sealed class ChatCompletionResponseBuilder
{
    private readonly IChatCompletionProvider _chatCompletionProvider;
    private readonly IChatCompletionCacheKeyFactory _chatCompletionCacheKeyFactory;
    private readonly IApplicationCache _applicationCache;
    private readonly IRagRuntimeSettings _ragRuntimeSettings;
    private readonly ResiliencePipeline _resiliencePipeline;

    public ChatCompletionResponseBuilder(
        IChatCompletionProvider chatCompletionProvider,
        IChatCompletionCacheKeyFactory chatCompletionCacheKeyFactory,
        IApplicationCache applicationCache,
        IRagRuntimeSettings ragRuntimeSettings,
        ResiliencePipeline resiliencePipeline)
    {
        _chatCompletionProvider = chatCompletionProvider;
        _chatCompletionCacheKeyFactory = chatCompletionCacheKeyFactory;
        _applicationCache = applicationCache;
        _ragRuntimeSettings = ragRuntimeSettings;
        _resiliencePipeline = resiliencePipeline;
    }

    public async Task<ChatCompletionResponse> BuildAsync(
        ChatRequestDto request,
        PromptTemplateDefinition template,
        AgenticChatPlan plan,
        ChatRetrievalContext retrievalContext,
        CancellationToken ct)
    {
        var responseMessage = template.InsufficientEvidenceMessage;
        ChatCompletionResult? completionResult = null;
        if (retrievalContext.EvidenceChunks.Count > 0 || retrievalContext.AllowsGeneralKnowledge)
        {
            var effectivePlan = new AgenticChatPlan
            {
                RequiresRetrieval = retrievalContext.EvidenceChunks.Count > 0,
                AllowsGeneralKnowledge = retrievalContext.AllowsGeneralKnowledge,
                PreferStreaming = plan.PreferStreaming,
                ExecutionMode = ResolveExecutionMode(retrievalContext.EvidenceChunks.Count > 0, retrievalContext.AllowsGeneralKnowledge)
            };

            var completionCacheKey = _chatCompletionCacheKeyFactory.Build(request, template, effectivePlan, retrievalContext.EvidenceChunks);
            completionResult = await _applicationCache.GetAsync<ChatCompletionResult>(completionCacheKey, ct);

            if (completionResult is null)
            {
                completionResult = await _resiliencePipeline.ExecuteAsync(async token => await _chatCompletionProvider.CompleteAsync(new ChatCompletionRequest
                {
                    Message = request.Message,
                    Template = template,
                    AllowGeneralKnowledge = retrievalContext.AllowsGeneralKnowledge,
                    RetrievedChunks = retrievalContext.EvidenceChunks
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

        return new ChatCompletionResponse
        {
            Message = responseMessage,
            CompletionResult = completionResult
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
}