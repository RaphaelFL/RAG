using System.Security.Claims;

namespace Chatbot.Mcp;

public interface IMcpServer
{
    Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, ClaimsPrincipal user, CancellationToken cancellationToken);
}