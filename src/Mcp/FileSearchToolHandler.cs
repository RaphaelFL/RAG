using System.Security.Claims;
using System.Text.Json;

namespace Chatbot.Mcp;

public sealed class FileSearchToolHandler : IMcpToolHandler
{
    private readonly IFileSearchTool _fileSearchTool;

    public FileSearchToolHandler(IFileSearchTool fileSearchTool)
    {
        _fileSearchTool = fileSearchTool;
    }

    public IReadOnlyCollection<string> SupportedToolNames { get; } = new[] { "file_search" };

    public async Task<JsonRpcResponse> HandleAsync(string? id, string toolName, JsonElement arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = McpRequestHelpers.GetTenantId(user);
        if (tenantId is null)
        {
            return McpResponseFactory.Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var query = McpRequestHelpers.ReadString(arguments, "query");
        var top = McpRequestHelpers.ReadInt(arguments, "top", 5);
        var result = await _fileSearchTool.SearchAsync(new FileSearchRequest
        {
            TenantId = tenantId.Value,
            Query = query,
            TopK = top,
            Filters = new Dictionary<string, string[]>()
        }, cancellationToken);

        return McpResponseFactory.Ok(id, new { matches = result.Matches });
    }
}