using Chatbot.Application.Abstractions;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace Chatbot.Infrastructure.Agentic;

internal sealed class SemanticKernelAgentPlanExecutor
{
    private readonly SemanticKernelAgentInputReader _inputReader;
    private readonly SemanticKernelToolInvoker _toolInvoker;
    private readonly SemanticKernelPromptAssemblyPlan _promptAssemblyPlan;

    public SemanticKernelAgentPlanExecutor(
        SemanticKernelAgentInputReader inputReader,
        SemanticKernelToolInvoker toolInvoker,
        SemanticKernelPromptAssemblyPlan promptAssemblyPlan)
    {
        _inputReader = inputReader;
        _toolInvoker = toolInvoker;
        _promptAssemblyPlan = promptAssemblyPlan;
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Kernel kernel, AgentRunRequest request, Guid agentRunId, int toolBudget, Action onToolExecuted, CancellationToken ct)
    {
        var query = _inputReader.ReadString(request.Input, "query");
        var question = _inputReader.ReadString(request.Input, "question", query);
        var systemInstructions = _inputReader.ReadString(request.Input, "systemInstructions", "Monte um prompt grounded, seguro e auditavel.");
        var topK = Math.Max(1, _inputReader.ReadInt(request.Input, "topK", 5));

        return request.AgentName switch
        {
            "FileSearchAgent" => _toolInvoker.InvokeJsonAsync(kernel, agentRunId, "file_search", new KernelArguments
            {
                ["tenantId"] = request.TenantId.ToString(),
                ["query"] = query,
                ["topK"] = topK.ToString(),
                ["filtersJson"] = JsonSerializer.Serialize(new Dictionary<string, string[]>(), SemanticKernelJson.Options)
            }, onToolExecuted, ct),
            "WebSearchAgent" => _toolInvoker.InvokeJsonAsync(kernel, agentRunId, "web_search", new KernelArguments
            {
                ["tenantId"] = request.TenantId.ToString(),
                ["query"] = query,
                ["topK"] = topK.ToString()
            }, onToolExecuted, ct),
            "CodeInterpreterAgent" => _toolInvoker.InvokeJsonAsync(kernel, agentRunId, "code_interpreter", new KernelArguments
            {
                ["tenantId"] = request.TenantId.ToString(),
                ["code"] = _inputReader.ReadString(request.Input, "code"),
                ["language"] = _inputReader.ReadString(request.Input, "language", "python")
            }, onToolExecuted, ct),
            "PromptAssemblyAgent" => _promptAssemblyPlan.ExecuteAsync(kernel, request.TenantId, agentRunId, question, systemInstructions, topK, toolBudget, onToolExecuted, ct),
            "OrchestratorAgent" => _promptAssemblyPlan.ExecuteAsync(kernel, request.TenantId, agentRunId, question, systemInstructions, topK, toolBudget, onToolExecuted, ct),
            _ => Task.FromResult(new Dictionary<string, object?>
            {
                ["message"] = "Agent ainda nao implementado nesta etapa.",
                ["agentName"] = request.AgentName,
                ["toolBudget"] = toolBudget
            })
        };
    }
}