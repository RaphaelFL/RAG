using Chatbot.Application.Abstractions;
using Chatbot.Domain.Entities;

namespace Chatbot.Application.Services;

public sealed class NoOpOperationalAuditReader : IOperationalAuditReader
{
    public Task<IReadOnlyCollection<RetrievalLogRecord>> ReadRetrievalLogsAsync(Guid tenantId, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyCollection<RetrievalLogRecord>>(Array.Empty<RetrievalLogRecord>());

    public Task<IReadOnlyCollection<PromptAssemblyRecord>> ReadPromptAssembliesAsync(Guid tenantId, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyCollection<PromptAssemblyRecord>>(Array.Empty<PromptAssemblyRecord>());

    public Task<IReadOnlyCollection<AgentRunRecord>> ReadAgentRunsAsync(Guid tenantId, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyCollection<AgentRunRecord>>(Array.Empty<AgentRunRecord>());

    public Task<IReadOnlyCollection<ToolExecutionRecord>> ReadToolExecutionsAsync(Guid tenantId, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyCollection<ToolExecutionRecord>>(Array.Empty<ToolExecutionRecord>());

    public Task<OperationalAuditFeedResult> ReadAuditFeedAsync(OperationalAuditFeedQuery query, CancellationToken ct)
        => Task.FromResult(new OperationalAuditFeedResult());
}