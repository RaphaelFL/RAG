using System.Security.Claims;
using System.Text.Json;
using Chatbot.Application.Abstractions;

namespace Chatbot.Mcp;

public sealed class AgentRunToolHandler : IMcpToolHandler
{
    private readonly IAgentRuntime _agentRuntime;

    public AgentRunToolHandler(IAgentRuntime agentRuntime)
    {
        _agentRuntime = agentRuntime;
    }

    public IReadOnlyCollection<string> SupportedToolNames { get; } = new[] { "agent_run" };

    public async Task<JsonRpcResponse> HandleAsync(string? id, string toolName, JsonElement arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = McpRequestHelpers.GetTenantId(user);
        if (tenantId is null)
        {
            return McpResponseFactory.Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var agentName = McpRequestHelpers.ReadString(arguments, "agentName");
        var objective = McpRequestHelpers.ReadString(arguments, "objective");
        var toolBudget = McpRequestHelpers.ReadInt(arguments, "toolBudget", 3);
        var result = await _agentRuntime.RunAsync(new AgentRunRequest
        {
            TenantId = tenantId.Value,
            AgentName = agentName,
            Objective = objective,
            ToolBudget = toolBudget,
            Input = new Dictionary<string, object?>
            {
                ["query"] = objective,
                ["question"] = objective
            }
        }, cancellationToken);

        return McpResponseFactory.Ok(id, result);
    }
}