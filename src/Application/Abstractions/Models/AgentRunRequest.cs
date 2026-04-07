namespace Chatbot.Application.Abstractions;

public sealed class AgentRunRequest
{
    public Guid TenantId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public int ToolBudget { get; set; }
    public Dictionary<string, object?> Input { get; set; } = new();
}
