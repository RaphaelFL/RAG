namespace Chatbot.Application.Contracts;

public sealed class AgentRunRequestDtoV2
{
    public string AgentName { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public int ToolBudget { get; set; } = 5;
    public Dictionary<string, object?> Input { get; set; } = new();
}
