using System.Security.Claims;
using System.Text.Json;

namespace Chatbot.Mcp;

public sealed class SummarizeSourcesToolHandler : IMcpToolHandler
{
    private readonly IRetrievalService _retrievalService;

    public SummarizeSourcesToolHandler(IRetrievalService retrievalService)
    {
        _retrievalService = retrievalService;
    }

    public IReadOnlyCollection<string> SupportedToolNames { get; } = new[] { "summarize_sources" };

    public async Task<JsonRpcResponse> HandleAsync(string? id, string toolName, JsonElement arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var query = McpRequestHelpers.ReadString(arguments, "query");
        var retrieval = await _retrievalService.RetrieveAsync(new RetrievalQueryDto
        {
            Query = query,
            TopK = 5,
            SemanticRanking = true
        }, cancellationToken);

        var distinctTitles = retrieval.Chunks
            .Select(chunk => chunk.DocumentTitle)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return McpResponseFactory.Ok(id, new
        {
            sourceCount = retrieval.Chunks.Count,
            documents = distinctTitles,
            summary = retrieval.Chunks.Count == 0
                ? "Nenhuma evidencia encontrada para resumir fontes."
                : $"Fontes encontradas em {distinctTitles.Count} documento(s): {string.Join(", ", distinctTitles.Take(3))}."
        });
    }
}