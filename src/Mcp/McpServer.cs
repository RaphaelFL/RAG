using System.Security.Claims;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Mcp;

public sealed class McpServer : IMcpServer
{
    private readonly IReadOnlyDictionary<string, IMcpMethodHandler> _methodHandlers;
    private readonly FeatureFlagOptions _featureFlags;

    public McpServer(
        IEnumerable<IMcpMethodHandler> methodHandlers,
        IOptions<FeatureFlagOptions> featureFlags)
    {
        _methodHandlers = methodHandlers
            .SelectMany(handler => handler.SupportedMethods.Select(method => new KeyValuePair<string, IMcpMethodHandler>(method, handler)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        _featureFlags = featureFlags.Value;
    }

    public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!_featureFlags.EnableMcp)
        {
            return McpResponseFactory.Error(request.Id, -32601, "MCP is disabled.");
        }

        if (!string.Equals(request.Jsonrpc, "2.0", StringComparison.Ordinal))
        {
            return McpResponseFactory.Error(request.Id, -32600, "Invalid JSON-RPC version.");
        }

        if (!_methodHandlers.TryGetValue(request.Method, out var handler))
        {
            return McpResponseFactory.Error(request.Id, -32601, "Method not found.");
        }

        return await handler.HandleAsync(request, user, cancellationToken);
    }
}
