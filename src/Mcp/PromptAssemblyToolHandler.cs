using System.Security.Claims;
using System.Text.Json;

namespace Chatbot.Mcp;

public sealed class PromptAssemblyToolHandler : IMcpToolHandler
{
    private readonly IFileSearchTool _fileSearchTool;
    private readonly IPromptAssembler _promptAssembler;

    public PromptAssemblyToolHandler(IFileSearchTool fileSearchTool, IPromptAssembler promptAssembler)
    {
        _fileSearchTool = fileSearchTool;
        _promptAssembler = promptAssembler;
    }

    public IReadOnlyCollection<string> SupportedToolNames { get; } = new[] { "assemble_prompt" };

    public async Task<JsonRpcResponse> HandleAsync(string? id, string toolName, JsonElement arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = McpRequestHelpers.GetTenantId(user);
        if (tenantId is null)
        {
            return McpResponseFactory.Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var question = McpRequestHelpers.ReadString(arguments, "question");
        var retrieval = await _fileSearchTool.SearchAsync(new FileSearchRequest
        {
            TenantId = tenantId.Value,
            Query = question,
            TopK = 5,
            Filters = new Dictionary<string, string[]>()
        }, cancellationToken);

        var prompt = await _promptAssembler.AssembleAsync(new PromptAssemblyRequest
        {
            TenantId = tenantId.Value,
            SystemInstructions = "Monte um prompt grounded, seguro e auditavel.",
            UserQuestion = question,
            Chunks = retrieval.Matches,
            MaxPromptTokens = 4000,
            AllowGeneralKnowledge = false
        }, cancellationToken);

        return McpResponseFactory.Ok(id, prompt);
    }
}