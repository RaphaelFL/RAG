namespace Chatbot.Application.Abstractions;

public interface IChatOrchestrator
{
    Task<ChatResponseDto> SendAsync(ChatRequestDto request, CancellationToken ct);
    IAsyncEnumerable<StreamingChatEventDto> StreamAsync(ChatRequestDto request, CancellationToken ct);
}