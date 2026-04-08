using Chatbot.Application.Abstractions;

namespace Chatbot.Application.Services;

internal sealed class ChatTurnPreparer : IChatTurnPreparer
{
    private readonly ChatRetrievalContextBuilder _chatRetrievalContextBuilder;
    private readonly ChatCompletionResponseBuilder _chatCompletionResponseBuilder;
    private readonly PreparedChatTurnFactory _preparedChatTurnFactory;

    public ChatTurnPreparer(
        ChatRetrievalContextBuilder chatRetrievalContextBuilder,
        ChatCompletionResponseBuilder chatCompletionResponseBuilder,
        PreparedChatTurnFactory preparedChatTurnFactory)
    {
        _chatRetrievalContextBuilder = chatRetrievalContextBuilder;
        _chatCompletionResponseBuilder = chatCompletionResponseBuilder;
        _preparedChatTurnFactory = preparedChatTurnFactory;
    }

    public async Task<PreparedChatTurn> PrepareAsync(
        ChatRequestDto request,
        PromptTemplateDefinition template,
        AgenticChatPlan plan,
        DateTime startTime,
        CancellationToken ct)
    {
        var retrievalContext = await _chatRetrievalContextBuilder.BuildAsync(request, plan, ct);
        var completionResponse = await _chatCompletionResponseBuilder.BuildAsync(request, template, plan, retrievalContext, ct);
        return _preparedChatTurnFactory.Create(template, startTime, retrievalContext, completionResponse);
    }
}