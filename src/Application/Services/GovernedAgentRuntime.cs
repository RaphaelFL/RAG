using System.Text.Json;
using Chatbot.Application.Configuration;
using Chatbot.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Chatbot.Application.Services;

public sealed class GovernedAgentRuntime : IAgentRuntime
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyDictionary<string, IGovernedAgentHandler> _handlers;
    private readonly AgentRuntimeOptions _options;
    private readonly IOperationalAuditWriter _operationalAuditWriter;

    public GovernedAgentRuntime(
        IEnumerable<IGovernedAgentHandler> handlers,
        IOptions<AgentRuntimeOptions> options,
        IOperationalAuditWriter operationalAuditWriter)
    {
        _handlers = handlers.ToDictionary(handler => handler.AgentName, StringComparer.OrdinalIgnoreCase);
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

        Dictionary<string, object?> result;
        if (_handlers.TryGetValue(request.AgentName, out var handler))
        {
            usedTools++;
            var execution = await handler.ExecuteAsync(request, ct);
            await WriteToolExecutionAsync(agentRunId, execution, ct);
            result = execution.Output;
        }
        else
        {
            result = new Dictionary<string, object?>
            {
                ["message"] = "Agent ainda nao implementado nesta etapa."
            };
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

    private Task WriteToolExecutionAsync(Guid agentRunId, AgentToolExecutionResult execution, CancellationToken ct)
    {
        return _operationalAuditWriter.WriteToolExecutionAsync(new ToolExecutionRecord
        {
            ToolExecutionId = Guid.NewGuid(),
            AgentRunId = agentRunId,
            ToolName = execution.ToolName,
            Status = execution.Status,
            InputJson = JsonSerializer.Serialize(execution.ToolRequest, SerializerOptions),
            OutputJson = JsonSerializer.Serialize(execution.ToolResponse, SerializerOptions),
            CreatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow
        }, ct);
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
