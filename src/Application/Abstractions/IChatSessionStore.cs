namespace Chatbot.Application.Abstractions;

public interface IChatSessionStore
{
    Task AppendTurnAsync(ChatSessionTurnRecord record, CancellationToken ct);
    Task<ChatSessionSnapshot?> GetAsync(Guid sessionId, Guid tenantId, CancellationToken ct);
}