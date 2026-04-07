using System.Security.Claims;

namespace Chatbot.Mcp;

public sealed class ToolCallMethodHandler : IMcpMethodHandler
{
    private readonly IReadOnlyDictionary<string, IMcpToolHandler> _toolHandlers;

    public ToolCallMethodHandler(IEnumerable<IMcpToolHandler> toolHandlers)
    {
        _toolHandlers = toolHandlers
            .SelectMany(handler => handler.SupportedToolNames.Select(toolName => new KeyValuePair<string, IMcpToolHandler>(toolName, handler)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> SupportedMethods { get; } = new[] { "tools/call" };

    public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var root = McpRequestHelpers.GetRoot(request.Params);
        var toolName = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return McpResponseFactory.Error(request.Id, -32602, "Unknown tool.");
        }

        if (!_toolHandlers.TryGetValue(toolName, out var handler))
        {
            return McpResponseFactory.Error(request.Id, -32602, "Unknown tool.");
        }

        return await handler.HandleAsync(request.Id, toolName, McpRequestHelpers.GetArguments(root), user, cancellationToken);
    }
}