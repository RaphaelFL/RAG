using System.Text;
using System.Text.Json;
using Chatbot.Application.Configuration;
using Chatbot.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Chatbot.Application.Services;

public sealed class GovernedAgentRuntime : IAgentRuntime
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IFileSearchTool _fileSearchTool;
    private readonly IWebSearchTool _webSearchTool;
    private readonly ICodeInterpreter _codeInterpreter;
    private readonly IPromptAssembler _promptAssembler;
    private readonly AgentRuntimeOptions _options;
    private readonly IOperationalAuditWriter _operationalAuditWriter;

    public GovernedAgentRuntime(
        IFileSearchTool fileSearchTool,
        IWebSearchTool webSearchTool,
        ICodeInterpreter codeInterpreter,
        IPromptAssembler promptAssembler,
        IOptions<AgentRuntimeOptions> options,
        IOperationalAuditWriter operationalAuditWriter)
    {
        _fileSearchTool = fileSearchTool;
        _webSearchTool = webSearchTool;
        _codeInterpreter = codeInterpreter;
        _promptAssembler = promptAssembler;
        _options = options.Value;
        _operationalAuditWriter = operationalAuditWriter;
    }

    public async Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken ct)
    {
        var agentRunId = Guid.NewGuid();
        var startedAtUtc = DateTime.UtcNow;
        var toolBudget = Math.Min(request.ToolBudget, _options.MaxToolBudget);
        var usedTools = 0;
        if (toolBudget <= 0)
        {
            var rejected = new AgentRunResult
            {
                AgentRunId = agentRunId,
                Status = "rejected",
                Output = new Dictionary<string, object?> { ["reason"] = "Tool budget invalido." }
            };

            await WriteAgentRunAsync(request, rejected, toolBudget, usedTools, startedAtUtc, ct);
            return rejected;
        }

        var result = new Dictionary<string, object?>();
        switch (request.AgentName)
        {
            case "FileSearchAgent":
            {
                var query = request.Input.TryGetValue("query", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
                var toolRequest = new FileSearchRequest
                {
                    TenantId = request.TenantId,
                    Query = query,
                    TopK = 5,
                    Filters = new Dictionary<string, string[]>()
                };
                usedTools++;
                var matches = await _fileSearchTool.SearchAsync(toolRequest, ct);
                await _operationalAuditWriter.WriteToolExecutionAsync(new ToolExecutionRecord
                {
                    ToolExecutionId = Guid.NewGuid(),
                    AgentRunId = agentRunId,
                    ToolName = "file_search",
                    Status = "completed",
                    InputJson = JsonSerializer.Serialize(toolRequest, SerializerOptions),
                    OutputJson = JsonSerializer.Serialize(new { matches = matches.Matches.Select(match => new { match.ChunkId, match.DocumentId, match.Score }) }, SerializerOptions),
                    CreatedAtUtc = DateTime.UtcNow,
                    CompletedAtUtc = DateTime.UtcNow
                }, ct);
                result["matches"] = matches.Matches.Select(match => new { match.ChunkId, match.DocumentId, match.Score }).ToArray();
                break;
            }
            case "WebSearchAgent":
            {
                var query = request.Input.TryGetValue("query", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
                var toolRequest = new WebSearchRequest { TenantId = request.TenantId, Query = query, TopK = 5 };
                usedTools++;
                var hits = await _webSearchTool.SearchAsync(toolRequest, ct);
                await _operationalAuditWriter.WriteToolExecutionAsync(new ToolExecutionRecord
                {
                    ToolExecutionId = Guid.NewGuid(),
                    AgentRunId = agentRunId,
                    ToolName = "web_search",
                    Status = "completed",
                    InputJson = JsonSerializer.Serialize(toolRequest, SerializerOptions),
                    OutputJson = JsonSerializer.Serialize(new { hits = hits.Hits }, SerializerOptions),
                    CreatedAtUtc = DateTime.UtcNow,
                    CompletedAtUtc = DateTime.UtcNow
                }, ct);
                result["hits"] = hits.Hits.ToArray();
                break;
            }
            case "CodeInterpreterAgent":
            {
                var code = request.Input.TryGetValue("code", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
                var toolRequest = new CodeInterpreterRequest { TenantId = request.TenantId, Code = code };
                usedTools++;
                var execution = await _codeInterpreter.ExecuteAsync(toolRequest, ct);
                await _operationalAuditWriter.WriteToolExecutionAsync(new ToolExecutionRecord
                {
                    ToolExecutionId = Guid.NewGuid(),
                    AgentRunId = agentRunId,
                    ToolName = "code_interpreter",
                    Status = execution.ExitCode == 0 ? "completed" : "failed",
                    InputJson = JsonSerializer.Serialize(toolRequest, SerializerOptions),
                    OutputJson = JsonSerializer.Serialize(execution, SerializerOptions),
                    CreatedAtUtc = DateTime.UtcNow,
                    CompletedAtUtc = DateTime.UtcNow
                }, ct);
                result["exitCode"] = execution.ExitCode;
                result["stderr"] = execution.StdErr;
                break;
            }
            case "PromptAssemblyAgent":
            {
                var question = request.Input.TryGetValue("question", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
                var toolRequest = new PromptAssemblyRequest
                {
                    TenantId = request.TenantId,
                    UserQuestion = question,
                    SystemInstructions = "Monte um prompt grounded, seguro e auditavel.",
                    Chunks = Array.Empty<RetrievedChunk>(),
                    MaxPromptTokens = 4000,
                    AllowGeneralKnowledge = false
                };
                usedTools++;
                var prompt = await _promptAssembler.AssembleAsync(toolRequest, ct);
                await _operationalAuditWriter.WriteToolExecutionAsync(new ToolExecutionRecord
                {
                    ToolExecutionId = Guid.NewGuid(),
                    AgentRunId = agentRunId,
                    ToolName = "assemble_prompt",
                    Status = "completed",
                    InputJson = JsonSerializer.Serialize(toolRequest, SerializerOptions),
                    OutputJson = JsonSerializer.Serialize(new { prompt.Prompt, prompt.EstimatedPromptTokens, prompt.IncludedChunkIds }, SerializerOptions),
                    CreatedAtUtc = DateTime.UtcNow,
                    CompletedAtUtc = DateTime.UtcNow
                }, ct);
                result["prompt"] = prompt.Prompt;
                break;
            }
            default:
                result["message"] = "Agent ainda nao implementado nesta etapa.";
                break;
        }

        var completed = new AgentRunResult
        {
            AgentRunId = agentRunId,
            Status = "completed",
            Output = result
        };

        await WriteAgentRunAsync(request, completed, toolBudget, usedTools, startedAtUtc, ct);
        return completed;
    }

    private Task WriteAgentRunAsync(AgentRunRequest request, AgentRunResult result, int toolBudget, int usedTools, DateTime startedAtUtc, CancellationToken ct)
    {
        return _operationalAuditWriter.WriteAgentRunAsync(new AgentRunRecord
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
}
