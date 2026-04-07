using System.Security.Claims;

namespace Chatbot.Mcp;

public interface IMcpMethodHandler
{
    IReadOnlyCollection<string> SupportedMethods { get; }
    Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, ClaimsPrincipal user, CancellationToken cancellationToken);
}