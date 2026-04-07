namespace Chatbot.Application.Abstractions;

public sealed class AgentRunResult
{
    public Guid AgentRunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object?> Output { get; set; } = new();
}
