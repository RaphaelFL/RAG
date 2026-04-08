using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Chatbot.Domain.Entities;
using Microsoft.SemanticKernel;

namespace Chatbot.Infrastructure.Agentic;

internal sealed class SemanticKernelToolInvoker
{
    private readonly IOperationalAuditWriter _operationalAuditWriter;

    public SemanticKernelToolInvoker(IOperationalAuditWriter operationalAuditWriter)
    {
        _operationalAuditWriter = operationalAuditWriter;
    }

    public async Task<Dictionary<string, object?>> InvokeJsonAsync(Kernel kernel, Guid agentRunId, string functionName, KernelArguments arguments, Action onToolExecuted, CancellationToken ct)
    {
        var payload = await InvokeRawAsync(kernel, agentRunId, functionName, arguments, onToolExecuted, ct);
        return DeserializePayload(payload);
    }

    public async Task<string> InvokeRawAsync(Kernel kernel, Guid agentRunId, string functionName, KernelArguments arguments, Action onToolExecuted, CancellationToken ct)
    {
        onToolExecuted();
        ChatbotTelemetry.AgentToolInvocations.Add(1, new KeyValuePair<string, object?>("agent.function", functionName));
        var result = await kernel.InvokeAsync("rag", functionName, arguments, ct);
        var payload = result.GetValue<string>() ?? result.ToString();

        await _operationalAuditWriter.WriteToolExecutionAsync(new ToolExecutionRecord
        {
            ToolExecutionId = Guid.NewGuid(),
            AgentRunId = agentRunId,
            ToolName = functionName,
            Status = "completed",
            InputJson = JsonSerializer.Serialize(arguments.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString()), SemanticKernelJson.Options),
            OutputJson = payload,
            CreatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow
        }, ct);

        return payload;
    }

    public static Dictionary<string, object?> DeserializePayload(string payload)
    {
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(payload, SemanticKernelJson.Options)
            ?? new Dictionary<string, object?> { ["payload"] = payload };
    }
}