namespace Chatbot.Application.Abstractions;

public interface IRetrievalAuditLogger
{
    Task WriteAsync(RetrievalAuditEntry entry, CancellationToken ct);
}