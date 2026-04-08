using Chatbot.Application.Abstractions;
using Chatbot.Domain.Entities;

namespace Chatbot.Application.Services;

public sealed class NoOpOperationalAuditWriter : IOperationalAuditWriter
{
    public Task WriteRetrievalLogAsync(RetrievalLogRecord record, CancellationToken ct) => Task.CompletedTask;

    public Task WritePromptAssemblyAsync(PromptAssemblyRecord record, CancellationToken ct) => Task.CompletedTask;

    public Task WriteAgentRunAsync(AgentRunRecord record, CancellationToken ct) => Task.CompletedTask;

    public Task WriteToolExecutionAsync(ToolExecutionRecord record, CancellationToken ct) => Task.CompletedTask;
}