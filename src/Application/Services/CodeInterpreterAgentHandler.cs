namespace Chatbot.Application.Services;

public sealed class CodeInterpreterAgentHandler : IGovernedAgentHandler
{
    private readonly ICodeInterpreter _codeInterpreter;

    public CodeInterpreterAgentHandler(ICodeInterpreter codeInterpreter)
    {
        _codeInterpreter = codeInterpreter;
    }

    public string AgentName => "CodeInterpreterAgent";

    public async Task<AgentToolExecutionResult> ExecuteAsync(AgentRunRequest request, CancellationToken ct)
    {
        var code = request.Input.TryGetValue("code", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        var toolRequest = new CodeInterpreterRequest
        {
            TenantId = request.TenantId,
            Code = code
        };

        var execution = await _codeInterpreter.ExecuteAsync(toolRequest, ct);
        return new AgentToolExecutionResult
        {
            ToolName = "code_interpreter",
            Status = execution.ExitCode == 0 ? "completed" : "failed",
            ToolRequest = toolRequest,
            ToolResponse = execution,
            Output = new Dictionary<string, object?>
            {
                ["exitCode"] = execution.ExitCode,
                ["stderr"] = execution.StdErr
            }
        };
    }
}