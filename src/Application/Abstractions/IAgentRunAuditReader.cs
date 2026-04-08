namespace Chatbot.Application.Abstractions;

public interface IAgentRunAuditReader
{
    Task<IReadOnlyCollection<AgentRunRecord>> ReadAgentRunsAsync(Guid tenantId, int limit, CancellationToken ct);
}