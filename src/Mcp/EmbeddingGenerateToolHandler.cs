using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Chatbot.Mcp;

public sealed class EmbeddingGenerateToolHandler : IMcpToolHandler
{
    private readonly IEmbeddingGenerationService _embeddingGenerationService;

    public EmbeddingGenerateToolHandler(IEmbeddingGenerationService embeddingGenerationService)
    {
        _embeddingGenerationService = embeddingGenerationService;
    }

    public IReadOnlyCollection<string> SupportedToolNames { get; } = new[] { "embedding_generate" };

    public async Task<JsonRpcResponse> HandleAsync(string? id, string toolName, JsonElement arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = McpRequestHelpers.GetTenantId(user);
        if (tenantId is null)
        {
            return McpResponseFactory.Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var text = McpRequestHelpers.ReadString(arguments, "text");
        var chunkId = McpRequestHelpers.ReadString(arguments, "chunkId", "ad-hoc");
        var contentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));

        var result = await _embeddingGenerationService.GenerateBatchAsync(new EmbeddingBatchRequest
        {
            Inputs = new[]
            {
                new EmbeddingInput
                {
                    ChunkId = chunkId,
                    DocumentId = Guid.Empty,
                    TenantId = tenantId.Value,
                    ContentHash = contentHash,
                    Text = text
                }
            }
        }, cancellationToken);

        return McpResponseFactory.Ok(id, result);
    }
}