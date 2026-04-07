namespace Chatbot.Domain.Entities;

public sealed class ToolExecutionRecord
{
    public Guid ToolExecutionId { get; set; }
    public Guid AgentRunId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
