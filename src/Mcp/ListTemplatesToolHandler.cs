using System.Security.Claims;
using System.Text.Json;

namespace Chatbot.Mcp;

public sealed class ListTemplatesToolHandler : IMcpToolHandler
{
    private readonly IPromptTemplateRegistry _promptTemplateRegistry;

    public ListTemplatesToolHandler(IPromptTemplateRegistry promptTemplateRegistry)
    {
        _promptTemplateRegistry = promptTemplateRegistry;
    }

    public IReadOnlyCollection<string> SupportedToolNames { get; } = new[] { "list_templates" };

    public Task<JsonRpcResponse> HandleAsync(string? id, string toolName, JsonElement arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        return Task.FromResult(McpResponseFactory.Ok(id, new
        {
            templates = _promptTemplateRegistry.ListAll().Select(template => new
            {
                templateId = template.TemplateId,
                version = template.Version
            })
        }));
    }
}