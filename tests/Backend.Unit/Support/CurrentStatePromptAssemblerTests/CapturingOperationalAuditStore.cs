using Chatbot.Application.Abstractions;
using Chatbot.Application.Services;
using FluentAssertions;
using Xunit;

namespace Backend.Unit.CurrentStatePromptAssemblerTestsSupport;

internal sealed class CapturingOperationalAuditStore : IOperationalAuditWriter, IOperationalAuditReader
{
    public Chatbot.Domain.Entities.PromptAssemblyRecord? LastPromptAssembly { get; private set; }

    public Task WriteRetrievalLogAsync(Chatbot.Domain.Entities.RetrievalLogRecord record, CancellationToken ct) => Task.CompletedTask;

    public Task WritePromptAssemblyAsync(Chatbot.Domain.Entities.PromptAssemblyRecord record, CancellationToken ct)
    {
        LastPromptAssembly = record;
        return Task.CompletedTask;
    }

    public Task WriteAgentRunAsync(Chatbot.Domain.Entities.AgentRunRecord record, CancellationToken ct) => Task.CompletedTask;

    public Task WriteToolExecutionAsync(Chatbot.Domain.Entities.ToolExecutionRecord record, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyCollection<Chatbot.Domain.Entities.RetrievalLogRecord>> ReadRetrievalLogsAsync(Guid tenantId, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyCollection<Chatbot.Domain.Entities.RetrievalLogRecord>>(Array.Empty<Chatbot.Domain.Entities.RetrievalLogRecord>());

    public Task<IReadOnlyCollection<Chatbot.Domain.Entities.PromptAssemblyRecord>> ReadPromptAssembliesAsync(Guid tenantId, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyCollection<Chatbot.Domain.Entities.PromptAssemblyRecord>>(Array.Empty<Chatbot.Domain.Entities.PromptAssemblyRecord>());

    public Task<IReadOnlyCollection<Chatbot.Domain.Entities.AgentRunRecord>> ReadAgentRunsAsync(Guid tenantId, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyCollection<Chatbot.Domain.Entities.AgentRunRecord>>(Array.Empty<Chatbot.Domain.Entities.AgentRunRecord>());

    public Task<IReadOnlyCollection<Chatbot.Domain.Entities.ToolExecutionRecord>> ReadToolExecutionsAsync(Guid tenantId, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyCollection<Chatbot.Domain.Entities.ToolExecutionRecord>>(Array.Empty<Chatbot.Domain.Entities.ToolExecutionRecord>());

    public Task<OperationalAuditFeedResult> ReadAuditFeedAsync(OperationalAuditFeedQuery query, CancellationToken ct)
        => Task.FromResult(new OperationalAuditFeedResult());
}
