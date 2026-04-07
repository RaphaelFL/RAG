namespace Chatbot.Application.Abstractions;

public sealed class AgenticChatPlan
{
    public bool RequiresRetrieval { get; init; }
    public bool AllowsGeneralKnowledge { get; init; }
    public bool PreferStreaming { get; init; }
    public string ExecutionMode { get; init; } = "grounded";
}
