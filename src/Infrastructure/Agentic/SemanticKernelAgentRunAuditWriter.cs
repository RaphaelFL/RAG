using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Domain.Entities;

namespace Chatbot.Infrastructure.Agentic;

internal sealed class SemanticKernelAgentRunAuditWriter
{
    private readonly IOperationalAuditWriter _operationalAuditWriter;

    public SemanticKernelAgentRunAuditWriter(IOperationalAuditWriter operationalAuditWriter)
    {
        _operationalAuditWriter = operationalAuditWriter;
    }

    public Task WriteAsync(AgentRunRequest request, AgentRunResult result, int toolBudget, int usedTools, DateTime startedAtUtc, CancellationToken ct)
    {
        return _operationalAuditWriter.WriteAgentRunAsync(new AgentRunRecord
        {
            AgentRunId = result.AgentRunId,
            TenantId = request.TenantId,
            AgentName = request.AgentName,
            Status = result.Status,
            ToolBudget = toolBudget,
            RemainingBudget = Math.Max(0, toolBudget - usedTools),
            InputJson = JsonSerializer.Serialize(new { request.Objective, request.Input }, SemanticKernelJson.Options),
            OutputJson = JsonSerializer.Serialize(result.Output, SemanticKernelJson.Options),
            CreatedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTime.UtcNow
        }, ct);
    }
}