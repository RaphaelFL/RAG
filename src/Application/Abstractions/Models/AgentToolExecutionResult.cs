namespace Chatbot.Application.Abstractions;

public sealed class AgentToolExecutionResult
{
    public string ToolName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public object? ToolRequest { get; set; }
    public object? ToolResponse { get; set; }
    public Dictionary<string, object?> Output { get; set; } = new();
}