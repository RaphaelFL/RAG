namespace Chatbot.Application.Abstractions;

public interface IOperationalAuditWriter
{
    Task WriteRetrievalLogAsync(RetrievalLogRecord record, CancellationToken ct);
    Task WritePromptAssemblyAsync(PromptAssemblyRecord record, CancellationToken ct);
    Task WriteAgentRunAsync(AgentRunRecord record, CancellationToken ct);
    Task WriteToolExecutionAsync(ToolExecutionRecord record, CancellationToken ct);
}