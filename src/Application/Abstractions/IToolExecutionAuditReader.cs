namespace Chatbot.Application.Abstractions;

public interface IToolExecutionAuditReader
{
    Task<IReadOnlyCollection<ToolExecutionRecord>> ReadToolExecutionsAsync(Guid tenantId, int limit, CancellationToken ct);
}