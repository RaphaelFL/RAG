namespace Chatbot.Application.Services;

internal sealed class ChatCompletionResponse
{
    public required string Message { get; init; }

    public ChatCompletionResult? CompletionResult { get; init; }
}