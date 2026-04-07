namespace Chatbot.Application.Abstractions;

public interface IGovernedAgentHandler
{
    string AgentName { get; }
    Task<AgentToolExecutionResult> ExecuteAsync(AgentRunRequest request, CancellationToken ct);
}