namespace Chatbot.Application.Contracts;

public sealed class AgentRunRequestDtoV2
{
    public string AgentName { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public int ToolBudget { get; set; } = 5;
    public Dictionary<string, object?> Input { get; set; } = new();
}

public sealed class AgentRunResponseDtoV2
{
    public Guid AgentRunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object?> Output { get; set; } = new();
}