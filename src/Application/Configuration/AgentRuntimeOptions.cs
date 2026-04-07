namespace Chatbot.Application.Configuration;

public sealed class AgentRuntimeOptions
{
    public bool Enabled { get; set; }
    public int MaxToolBudget { get; set; } = 8;
    public int MaxDepth { get; set; } = 4;
    public int DefaultTimeoutSeconds { get; set; } = 30;
}
