namespace Chatbot.Domain.Entities;

public sealed class ConversationRecord
{
    public Guid ConversationId { get; set; }
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class ConversationMessageRecord
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

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