using System.Security.Claims;

namespace Chatbot.Mcp;

public sealed class PromptListMethodHandler : IMcpMethodHandler
{
    private readonly IPromptTemplateRegistry _promptTemplateRegistry;

    public PromptListMethodHandler(IPromptTemplateRegistry promptTemplateRegistry)
    {
        _promptTemplateRegistry = promptTemplateRegistry;
    }

    public IReadOnlyCollection<string> SupportedMethods { get; } = new[] { "prompts/list" };

    public Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        return Task.FromResult(McpResponseFactory.Ok(request.Id, new
        {
            prompts = _promptTemplateRegistry.ListAll().Select(template => new
            {
                name = template.TemplateId,
                version = template.Version
            })
        }));
    }
}