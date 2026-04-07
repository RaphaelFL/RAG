using System.Security.Claims;
using System.Text.Json;
using Chatbot.Mcp;

namespace Backend.Unit.McpServerTestsSupport;

internal sealed class CapturingMcpToolHandler : IMcpToolHandler
{
    private readonly JsonRpcResponse _response;

    public CapturingMcpToolHandler(string toolName, JsonRpcResponse response)
    {
        SupportedToolNames = new[] { toolName };
        _response = response;
    }

    public IReadOnlyCollection<string> SupportedToolNames { get; }

    public int CallCount { get; private set; }

    public string? LastToolName { get; private set; }

    public Task<JsonRpcResponse> HandleAsync(string? id, string toolName, JsonElement arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        CallCount++;
        LastToolName = toolName;
        return Task.FromResult(_response);
    }
}