namespace Chatbot.Application.Abstractions;

public interface IRetrievalAuditReader
{
    Task<IReadOnlyCollection<RetrievalLogRecord>> ReadRetrievalLogsAsync(Guid tenantId, int limit, CancellationToken ct);
}