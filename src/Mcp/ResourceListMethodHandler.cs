using System.Security.Claims;

namespace Chatbot.Mcp;

public sealed class ResourceListMethodHandler : IMcpMethodHandler
{
    public IReadOnlyCollection<string> SupportedMethods { get; } = new[] { "resources/list" };

    public Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        return Task.FromResult(McpResponseFactory.Ok(request.Id, new
        {
            resources = new[]
            {
                new { uri = "rag://knowledge-base-schema", name = "Knowledge base schema" },
                new { uri = "rag://prompt-catalog", name = "Prompt catalog" },
                new { uri = "rag://retrieval-policies", name = "Retrieval policies" },
                new { uri = "rag://embedding-model-info", name = "Embedding model info" },
                new { uri = "rag://runtime-capabilities", name = "Runtime capabilities" },
                new { uri = "rag://vector-store-stats", name = "Vector store stats" },
                new { uri = "rag://prompt-assembly-policy", name = "Prompt assembly policy" }
            }
        }));
    }
}