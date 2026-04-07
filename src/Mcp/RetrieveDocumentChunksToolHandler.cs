using System.Security.Claims;
using System.Text.Json;

namespace Chatbot.Mcp;

public sealed class RetrieveDocumentChunksToolHandler : IMcpToolHandler
{
    private readonly IRetrievalService _retrievalService;

    public RetrieveDocumentChunksToolHandler(IRetrievalService retrievalService)
    {
        _retrievalService = retrievalService;
    }

    public IReadOnlyCollection<string> SupportedToolNames { get; } = new[] { "retrieve_document_chunks" };

    public async Task<JsonRpcResponse> HandleAsync(string? id, string toolName, JsonElement arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var query = McpRequestHelpers.ReadString(arguments, "query");
        var top = McpRequestHelpers.ReadInt(arguments, "top", 5);
        var result = await _retrievalService.RetrieveAsync(new RetrievalQueryDto
        {
            Query = query,
            TopK = top,
            SemanticRanking = true
        }, cancellationToken);

        return McpResponseFactory.Ok(id, new
        {
            chunks = result.Chunks,
            strategy = result.RetrievalStrategy,
            latencyMs = result.LatencyMs
        });
    }
}