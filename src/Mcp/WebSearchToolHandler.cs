using System.Security.Claims;
using System.Text.Json;

namespace Chatbot.Mcp;

public sealed class WebSearchToolHandler : IMcpToolHandler
{
    private readonly IWebSearchTool _webSearchTool;

    public WebSearchToolHandler(IWebSearchTool webSearchTool)
    {
        _webSearchTool = webSearchTool;
    }

    public IReadOnlyCollection<string> SupportedToolNames { get; } = new[] { "web_search" };

    public async Task<JsonRpcResponse> HandleAsync(string? id, string toolName, JsonElement arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = McpRequestHelpers.GetTenantId(user);
        if (tenantId is null)
        {
            return McpResponseFactory.Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var query = McpRequestHelpers.ReadString(arguments, "query");
        var top = McpRequestHelpers.ReadInt(arguments, "top", 5);
        var result = await _webSearchTool.SearchAsync(new WebSearchRequest
        {
            TenantId = tenantId.Value,
            Query = query,
            TopK = top
        }, cancellationToken);

        return McpResponseFactory.Ok(id, result);
    }
}