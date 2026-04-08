using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace Chatbot.Infrastructure.Agentic;

public sealed class SemanticKernelAgentRuntime : IAgentRuntime
{
    private readonly IFileSearchTool _fileSearchTool;
    private readonly IWebSearchTool _webSearchTool;
    private readonly ICodeInterpreter _codeInterpreter;
    private readonly IPromptAssembler _promptAssembler;
    private readonly SemanticKernelAgentPlanExecutor _planExecutor;
    private readonly SemanticKernelAgentRunAuditWriter _auditWriter;
    private readonly AgentRuntimeOptions _options;
    private readonly ILogger<SemanticKernelAgentRuntime> _logger;

    public SemanticKernelAgentRuntime(
        IFileSearchTool fileSearchTool,
        IWebSearchTool webSearchTool,
        ICodeInterpreter codeInterpreter,
        IPromptAssembler promptAssembler,
        IOperationalAuditWriter operationalAuditWriter,
        IOptions<AgentRuntimeOptions> options,
        ILogger<SemanticKernelAgentRuntime> logger)
    {
        _fileSearchTool = fileSearchTool;
        _webSearchTool = webSearchTool;
        _codeInterpreter = codeInterpreter;
        _promptAssembler = promptAssembler;
        var toolInvoker = new SemanticKernelToolInvoker(operationalAuditWriter);
        _planExecutor = new SemanticKernelAgentPlanExecutor(
            new SemanticKernelAgentInputReader(),
            toolInvoker,
            new SemanticKernelPromptAssemblyPlan(promptAssembler, operationalAuditWriter, toolInvoker));
        _auditWriter = new SemanticKernelAgentRunAuditWriter(operationalAuditWriter);
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken ct)
    {
        var agentRunId = Guid.NewGuid();
        var startedAtUtc = DateTime.UtcNow;
        var usedTools = 0;
        if (!_options.Enabled)
        {
            var disabled = new AgentRunResult
            {
                AgentRunId = agentRunId,
                Status = "disabled",
                Output = new Dictionary<string, object?> { ["reason"] = "Agent runtime desabilitado nesta configuracao." }
            };

            await _auditWriter.WriteAsync(request, disabled, 0, usedTools, startedAtUtc, ct);
            return disabled;
        }

        var toolBudget = Math.Min(Math.Max(1, request.ToolBudget), _options.MaxToolBudget);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.DefaultTimeoutSeconds)));

        var kernel = Kernel.CreateBuilder().Build();
        kernel.ImportPluginFromObject(new RagAgentKernelPlugin(_fileSearchTool, _webSearchTool, _codeInterpreter, _promptAssembler), "rag");

        try
        {
            var output = await _planExecutor.ExecuteAsync(kernel, request, agentRunId, toolBudget, () => usedTools++, timeoutCts.Token);
            var completed = new AgentRunResult
            {
                AgentRunId = agentRunId,
                Status = "completed",
                Output = output
            };

            await _auditWriter.WriteAsync(request, completed, toolBudget, usedTools, startedAtUtc, ct);
            return completed;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            var timeout = new AgentRunResult
            {
                AgentRunId = agentRunId,
                Status = "timeout",
                Output = new Dictionary<string, object?> { ["reason"] = "Tempo maximo do agent runtime excedido." }
            };

            await _auditWriter.WriteAsync(request, timeout, toolBudget, usedTools, startedAtUtc, ct);
            return timeout;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na execucao do agent {AgentName}.", request.AgentName);
            var failed = new AgentRunResult
            {
                AgentRunId = agentRunId,
                Status = "failed",
                Output = new Dictionary<string, object?> { ["reason"] = ex.Message }
            };

            await _auditWriter.WriteAsync(request, failed, toolBudget, usedTools, startedAtUtc, ct);
            return failed;
        }
    }
}
