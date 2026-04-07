using System.Security.Claims;
using System.Text.Json;

namespace Chatbot.Mcp;

public interface IMcpToolHandler
{
    IReadOnlyCollection<string> SupportedToolNames { get; }
    Task<JsonRpcResponse> HandleAsync(string? id, string toolName, JsonElement arguments, ClaimsPrincipal user, CancellationToken cancellationToken);
}