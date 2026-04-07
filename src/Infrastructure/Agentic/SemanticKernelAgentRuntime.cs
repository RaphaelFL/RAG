using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Observability;
using Chatbot.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace Chatbot.Infrastructure.Agentic;

public sealed class SemanticKernelAgentRuntime : IAgentRuntime
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IFileSearchTool _fileSearchTool;
    private readonly IWebSearchTool _webSearchTool;
    private readonly ICodeInterpreter _codeInterpreter;
    private readonly IPromptAssembler _promptAssembler;
    private readonly IOperationalAuditStore _operationalAuditStore;
    private readonly AgentRuntimeOptions _options;
    private readonly ILogger<SemanticKernelAgentRuntime> _logger;

    public SemanticKernelAgentRuntime(
        IFileSearchTool fileSearchTool,
        IWebSearchTool webSearchTool,
        ICodeInterpreter codeInterpreter,
        IPromptAssembler promptAssembler,
        IOperationalAuditStore operationalAuditStore,
        IOptions<AgentRuntimeOptions> options,
        ILogger<SemanticKernelAgentRuntime> logger)
    {
        _fileSearchTool = fileSearchTool;
        _webSearchTool = webSearchTool;
        _codeInterpreter = codeInterpreter;
        _promptAssembler = promptAssembler;
        _operationalAuditStore = operationalAuditStore;
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

            await WriteAgentRunAsync(request, disabled, 0, usedTools, startedAtUtc, ct);
            return disabled;
        }

        var toolBudget = Math.Min(Math.Max(1, request.ToolBudget), _options.MaxToolBudget);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.DefaultTimeoutSeconds)));

        var kernel = Kernel.CreateBuilder().Build();
        kernel.ImportPluginFromObject(new RagAgentKernelPlugin(_fileSearchTool, _webSearchTool, _codeInterpreter, _promptAssembler), "rag");

        try
        {
            var output = await ExecutePlanAsync(kernel, request, agentRunId, toolBudget, () => usedTools++, timeoutCts.Token);
            var completed = new AgentRunResult
            {
                AgentRunId = agentRunId,
                Status = "completed",
                Output = output
            };

            await WriteAgentRunAsync(request, completed, toolBudget, usedTools, startedAtUtc, ct);
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

            await WriteAgentRunAsync(request, timeout, toolBudget, usedTools, startedAtUtc, ct);
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

            await WriteAgentRunAsync(request, failed, toolBudget, usedTools, startedAtUtc, ct);
            return failed;
        }
    }

    private async Task<Dictionary<string, object?>> ExecutePlanAsync(Kernel kernel, AgentRunRequest request, Guid agentRunId, int toolBudget, Action onToolExecuted, CancellationToken ct)
    {
        var query = ReadStringInput(request.Input, "query");
        var question = ReadStringInput(request.Input, "question", query);
        var systemInstructions = ReadStringInput(request.Input, "systemInstructions", "Monte um prompt grounded, seguro e auditavel.");
        var topK = Math.Max(1, ReadIntInput(request.Input, "topK", 5));

        return request.AgentName switch
        {
            "FileSearchAgent" => await InvokeJsonAsync(kernel, agentRunId, "file_search", new KernelArguments
            {
                ["tenantId"] = request.TenantId.ToString(),
                ["query"] = query,
                ["topK"] = topK.ToString(),
                ["filtersJson"] = JsonSerializer.Serialize(new Dictionary<string, string[]>(), SerializerOptions)
            }, onToolExecuted, ct),
            "WebSearchAgent" => await InvokeJsonAsync(kernel, agentRunId, "web_search", new KernelArguments
            {
                ["tenantId"] = request.TenantId.ToString(),
                ["query"] = query,
                ["topK"] = topK.ToString()
            }, onToolExecuted, ct),
            "CodeInterpreterAgent" => await InvokeJsonAsync(kernel, agentRunId, "code_interpreter", new KernelArguments
            {
                ["tenantId"] = request.TenantId.ToString(),
                ["code"] = ReadStringInput(request.Input, "code"),
                ["language"] = ReadStringInput(request.Input, "language", "python")
            }, onToolExecuted, ct),
            "PromptAssemblyAgent" => await RunPromptAssemblyPlanAsync(kernel, request.TenantId, agentRunId, question, systemInstructions, topK, toolBudget, onToolExecuted, ct),
            "OrchestratorAgent" => await RunPromptAssemblyPlanAsync(kernel, request.TenantId, agentRunId, question, systemInstructions, topK, toolBudget, onToolExecuted, ct),
            _ => new Dictionary<string, object?>
            {
                ["message"] = "Agent ainda nao implementado nesta etapa.",
                ["agentName"] = request.AgentName,
                ["toolBudget"] = toolBudget
            }
        };
    }

    private async Task<Dictionary<string, object?>> RunPromptAssemblyPlanAsync(Kernel kernel, Guid tenantId, Guid agentRunId, string question, string systemInstructions, int topK, int toolBudget, Action onToolExecuted, CancellationToken ct)
    {
        if (toolBudget < 2)
        {
            return new Dictionary<string, object?>
            {
                ["reason"] = "Tool budget insuficiente para retrieval + prompt assembly. Minimo requerido: 2."
            };
        }

        var searchArguments = new KernelArguments
        {
            ["tenantId"] = tenantId.ToString(),
            ["query"] = question,
            ["topK"] = topK.ToString(),
            ["filtersJson"] = JsonSerializer.Serialize(new Dictionary<string, string[]>(), SerializerOptions)
        };
        var searchPayload = await InvokeRawAsync(kernel, agentRunId, "file_search", searchArguments, onToolExecuted, ct);
        var searchResult = DeserializePayload(searchPayload);
        var retrievedChunks = DeserializeRetrievedChunks(searchPayload);
        var promptRequest = new PromptAssemblyRequest
        {
            TenantId = tenantId,
            UserQuestion = question,
            SystemInstructions = systemInstructions,
            Chunks = retrievedChunks,
            MaxPromptTokens = 4000,
            AllowGeneralKnowledge = false
        };
        onToolExecuted();
        var promptAssembly = await _promptAssembler.AssembleAsync(promptRequest, ct);
        await _operationalAuditStore.WriteToolExecutionAsync(new ToolExecutionRecord
        {
            ToolExecutionId = Guid.NewGuid(),
            AgentRunId = agentRunId,
            ToolName = "assemble_prompt",
            Status = "completed",
            InputJson = JsonSerializer.Serialize(promptRequest, SerializerOptions),
            OutputJson = JsonSerializer.Serialize(new
            {
                promptAssembly.Prompt,
                promptAssembly.IncludedChunkIds,
                promptAssembly.EstimatedPromptTokens,
                promptAssembly.HumanReadableCitations
            }, SerializerOptions),
            CreatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow
        }, ct);

        var promptResult = new Dictionary<string, object?>
        {
            ["prompt"] = promptAssembly.Prompt,
            ["includedChunkIds"] = promptAssembly.IncludedChunkIds,
            ["estimatedPromptTokens"] = promptAssembly.EstimatedPromptTokens,
            ["citations"] = promptAssembly.HumanReadableCitations
        };

        return new Dictionary<string, object?>(promptResult, StringComparer.OrdinalIgnoreCase)
        {
            ["retrieval"] = searchResult
        };
    }

    private async Task<Dictionary<string, object?>> InvokeJsonAsync(Kernel kernel, Guid agentRunId, string functionName, KernelArguments arguments, Action onToolExecuted, CancellationToken ct)
    {
        var payload = await InvokeRawAsync(kernel, agentRunId, functionName, arguments, onToolExecuted, ct);
        return DeserializePayload(payload);
    }

    private async Task<string> InvokeRawAsync(Kernel kernel, Guid agentRunId, string functionName, KernelArguments arguments, Action onToolExecuted, CancellationToken ct)
    {
        onToolExecuted();
        ChatbotTelemetry.AgentToolInvocations.Add(1, new KeyValuePair<string, object?>("agent.function", functionName));
        var result = await kernel.InvokeAsync("rag", functionName, arguments, ct);
        var payload = result.GetValue<string>() ?? result.ToString();

        await _operationalAuditStore.WriteToolExecutionAsync(new ToolExecutionRecord
        {
            ToolExecutionId = Guid.NewGuid(),
            AgentRunId = agentRunId,
            ToolName = functionName,
            Status = "completed",
            InputJson = JsonSerializer.Serialize(arguments.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString()), SerializerOptions),
            OutputJson = payload,
            CreatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow
        }, ct);

        return payload;
    }

    private Task WriteAgentRunAsync(AgentRunRequest request, AgentRunResult result, int toolBudget, int usedTools, DateTime startedAtUtc, CancellationToken ct)
    {
        return _operationalAuditStore.WriteAgentRunAsync(new AgentRunRecord
        {
            AgentRunId = result.AgentRunId,
            TenantId = request.TenantId,
            AgentName = request.AgentName,
            Status = result.Status,
            ToolBudget = toolBudget,
            RemainingBudget = Math.Max(0, toolBudget - usedTools),
            InputJson = JsonSerializer.Serialize(new { request.Objective, request.Input }, SerializerOptions),
            OutputJson = JsonSerializer.Serialize(result.Output, SerializerOptions),
            CreatedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTime.UtcNow
        }, ct);
    }

    private static Dictionary<string, object?> DeserializePayload(string payload)
    {
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(payload, SerializerOptions)
            ?? new Dictionary<string, object?> { ["payload"] = payload };
    }

    private static string ReadStringInput(Dictionary<string, object?> input, string key, string fallback = "")
    {
        return input.TryGetValue(key, out var value) ? value?.ToString() ?? fallback : fallback;
    }

    private static int ReadIntInput(Dictionary<string, object?> input, string key, int fallback)
    {
        return input.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var parsed)
            ? parsed
            : fallback;
    }

    private static RetrievedChunk[] DeserializeRetrievedChunks(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("matches", out var matchesElement))
            {
                var matches = JsonSerializer.Deserialize<List<RetrievedChunkEnvelope>>(matchesElement.GetRawText(), SerializerOptions);
                return matches?.Select(MapRetrievedChunk).ToArray() ?? Array.Empty<RetrievedChunk>();
            }
        }
        catch
        {
        }

        return Array.Empty<RetrievedChunk>();
    }

    private static RetrievedChunk MapRetrievedChunk(RetrievedChunkEnvelope item)
    {
        return new RetrievedChunk
        {
            ChunkId = item.ChunkId ?? string.Empty,
            DocumentId = item.DocumentId,
            Score = item.Score,
            Text = item.Text ?? string.Empty,
            Metadata = item.Metadata ?? new Dictionary<string, string>()
        };
    }
}
