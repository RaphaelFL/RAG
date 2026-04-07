namespace Chatbot.Application.Abstractions;

public interface IAgentRuntime
{
    Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken ct);
}