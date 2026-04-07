using System.Security.Claims;
using System.Text.Json;

namespace Chatbot.Mcp;

public sealed class SearchToolHandler : IMcpToolHandler
{
    private readonly ISearchQueryService _searchQueryService;

    public SearchToolHandler(ISearchQueryService searchQueryService)
    {
        _searchQueryService = searchQueryService;
    }

    public IReadOnlyCollection<string> SupportedToolNames { get; } = new[] { "search", "search_knowledge" };

    public async Task<JsonRpcResponse> HandleAsync(string? id, string toolName, JsonElement arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var query = McpRequestHelpers.ReadString(arguments, "query");
        var top = McpRequestHelpers.ReadInt(arguments, "top", 5);
        var result = await _searchQueryService.QueryAsync(new SearchQueryRequestDto
        {
            Query = query,
            Top = top
        }, cancellationToken);

        return McpResponseFactory.Ok(id, result);
    }
}