using System.Security.Claims;
using System.Text.Json;
using Chatbot.Application.Abstractions;

namespace Chatbot.Mcp;

public sealed class CodeInterpreterToolHandler : IMcpToolHandler
{
    private readonly ICodeInterpreter _codeInterpreter;

    public CodeInterpreterToolHandler(ICodeInterpreter codeInterpreter)
    {
        _codeInterpreter = codeInterpreter;
    }

    public IReadOnlyCollection<string> SupportedToolNames { get; } = new[] { "code_interpreter" };

    public async Task<JsonRpcResponse> HandleAsync(string? id, string toolName, JsonElement arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = McpRequestHelpers.GetTenantId(user);
        if (tenantId is null)
        {
            return McpResponseFactory.Error(id, -32003, "A valid tenant_id claim is required.");
        }

        var code = McpRequestHelpers.ReadString(arguments, "code");
        var language = McpRequestHelpers.ReadString(arguments, "language", "python");
        var result = await _codeInterpreter.ExecuteAsync(new CodeInterpreterRequest
        {
            TenantId = tenantId.Value,
            Code = code,
            Language = language
        }, cancellationToken);

        return McpResponseFactory.Ok(id, result);
    }
}