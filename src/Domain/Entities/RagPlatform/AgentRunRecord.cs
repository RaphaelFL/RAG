namespace Chatbot.Domain.Entities;

public sealed class AgentRunRecord
{
    public Guid AgentRunId { get; set; }
    public Guid TenantId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ToolBudget { get; set; }
    public int RemainingBudget { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
