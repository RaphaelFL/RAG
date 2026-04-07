using System.Security.Claims;
using System.Text.Json;

namespace Chatbot.Mcp;

public sealed class ReindexToolHandler : IMcpToolHandler
{
    private readonly IDocumentReindexService _documentReindexService;

    public ReindexToolHandler(IDocumentReindexService documentReindexService)
    {
        _documentReindexService = documentReindexService;
    }

    public IReadOnlyCollection<string> SupportedToolNames { get; } = new[] { "reindex", "reindex_document" };

    public async Task<JsonRpcResponse> HandleAsync(string? id, string toolName, JsonElement arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        if (role is not ("Analyst" or "TenantAdmin" or "PlatformAdmin"))
        {
            return McpResponseFactory.Error(id, -32003, "Administrative scope is required.");
        }

        var tenantId = McpRequestHelpers.GetTenantId(user);
        if (tenantId is null)
        {
            return McpResponseFactory.Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var mode = McpRequestHelpers.ReadString(arguments, "mode", "incremental");
        var documentIds = arguments.TryGetProperty("documentIds", out var idsProperty)
            ? idsProperty.EnumerateArray().Select(item => item.GetGuid()).ToList()
            : new List<Guid>();
        var includeAllTenantDocuments = arguments.TryGetProperty("includeAllTenantDocuments", out var includeAllProperty)
            ? includeAllProperty.GetBoolean()
            : documentIds.Count == 0;

        var result = await _documentReindexService.ReindexAsync(new BulkReindexRequestDto
        {
            DocumentIds = documentIds,
            IncludeAllTenantDocuments = includeAllTenantDocuments,
            Mode = mode
        }, tenantId.Value, cancellationToken);

        return McpResponseFactory.Ok(id, result);
    }
}