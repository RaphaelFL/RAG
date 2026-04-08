using Chatbot.Application.Abstractions;
using Chatbot.Domain.Entities;

namespace Chatbot.Application.Services;

public sealed class NoOpOperationalAuditStore : IOperationalAuditWriter, IOperationalAuditReader
{
    private readonly NoOpOperationalAuditWriter _writer = new();
    private readonly NoOpOperationalAuditReader _reader = new();

    public Task WriteRetrievalLogAsync(RetrievalLogRecord record, CancellationToken ct)
        => _writer.WriteRetrievalLogAsync(record, ct);

    public Task WritePromptAssemblyAsync(PromptAssemblyRecord record, CancellationToken ct)
        => _writer.WritePromptAssemblyAsync(record, ct);

    public Task WriteAgentRunAsync(AgentRunRecord record, CancellationToken ct)
        => _writer.WriteAgentRunAsync(record, ct);

    public Task WriteToolExecutionAsync(ToolExecutionRecord record, CancellationToken ct)
        => _writer.WriteToolExecutionAsync(record, ct);

    public Task<IReadOnlyCollection<RetrievalLogRecord>> ReadRetrievalLogsAsync(Guid tenantId, int limit, CancellationToken ct)
        => _reader.ReadRetrievalLogsAsync(tenantId, limit, ct);

    public Task<IReadOnlyCollection<PromptAssemblyRecord>> ReadPromptAssembliesAsync(Guid tenantId, int limit, CancellationToken ct)
        => _reader.ReadPromptAssembliesAsync(tenantId, limit, ct);

    public Task<IReadOnlyCollection<AgentRunRecord>> ReadAgentRunsAsync(Guid tenantId, int limit, CancellationToken ct)
        => _reader.ReadAgentRunsAsync(tenantId, limit, ct);

    public Task<IReadOnlyCollection<ToolExecutionRecord>> ReadToolExecutionsAsync(Guid tenantId, int limit, CancellationToken ct)
        => _reader.ReadToolExecutionsAsync(tenantId, limit, ct);

    public Task<OperationalAuditFeedResult> ReadAuditFeedAsync(OperationalAuditFeedQuery query, CancellationToken ct)
        => _reader.ReadAuditFeedAsync(query, ct);
}