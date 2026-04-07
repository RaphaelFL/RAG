namespace Chatbot.Application.Contracts;

public sealed class AgentRunResponseDtoV2
{
    public Guid AgentRunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object?> Output { get; set; } = new();
}
