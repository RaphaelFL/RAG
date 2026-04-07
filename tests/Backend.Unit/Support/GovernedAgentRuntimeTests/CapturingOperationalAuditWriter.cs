using Chatbot.Application.Abstractions;
using Chatbot.Domain.Entities;

namespace Backend.Unit.GovernedAgentRuntimeTestsSupport;

internal sealed class CapturingOperationalAuditWriter : IOperationalAuditWriter
{
    public List<ToolExecutionRecord> ToolExecutions { get; } = new();

    public List<AgentRunRecord> AgentRuns { get; } = new();

    public Task WriteRetrievalLogAsync(RetrievalLogRecord record, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task WritePromptAssemblyAsync(PromptAssemblyRecord record, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task WriteAgentRunAsync(AgentRunRecord record, CancellationToken ct)
    {
        AgentRuns.Add(record);
        return Task.CompletedTask;
    }

    public Task WriteToolExecutionAsync(ToolExecutionRecord record, CancellationToken ct)
    {
        ToolExecutions.Add(record);
        return Task.CompletedTask;
    }
}