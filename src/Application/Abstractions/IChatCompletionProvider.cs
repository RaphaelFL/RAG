namespace Chatbot.Application.Abstractions;

public interface IChatCompletionProvider
{
    Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct);
}