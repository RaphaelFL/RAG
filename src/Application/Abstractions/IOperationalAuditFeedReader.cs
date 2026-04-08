namespace Chatbot.Application.Abstractions;

public interface IOperationalAuditFeedReader
{
    Task<OperationalAuditFeedResult> ReadAuditFeedAsync(OperationalAuditFeedQuery query, CancellationToken ct);
}