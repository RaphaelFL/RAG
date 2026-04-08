using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Domain.Entities;
using Microsoft.SemanticKernel;

namespace Chatbot.Infrastructure.Agentic;

internal sealed class SemanticKernelPromptAssemblyPlan
{
    private readonly IPromptAssembler _promptAssembler;
    private readonly IOperationalAuditWriter _operationalAuditWriter;
    private readonly SemanticKernelToolInvoker _toolInvoker;

    public SemanticKernelPromptAssemblyPlan(
        IPromptAssembler promptAssembler,
        IOperationalAuditWriter operationalAuditWriter,
        SemanticKernelToolInvoker toolInvoker)
    {
        _promptAssembler = promptAssembler;
        _operationalAuditWriter = operationalAuditWriter;
        _toolInvoker = toolInvoker;
    }

    public async Task<Dictionary<string, object?>> ExecuteAsync(
        Kernel kernel,
        Guid tenantId,
        Guid agentRunId,
        string question,
        string systemInstructions,
        int topK,
        int toolBudget,
        Action onToolExecuted,
        CancellationToken ct)
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
            ["filtersJson"] = JsonSerializer.Serialize(new Dictionary<string, string[]>(), SemanticKernelJson.Options)
        };
        var searchPayload = await _toolInvoker.InvokeRawAsync(kernel, agentRunId, "file_search", searchArguments, onToolExecuted, ct);
        var searchResult = SemanticKernelToolInvoker.DeserializePayload(searchPayload);
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
        await _operationalAuditWriter.WriteToolExecutionAsync(new ToolExecutionRecord
        {
            ToolExecutionId = Guid.NewGuid(),
            AgentRunId = agentRunId,
            ToolName = "assemble_prompt",
            Status = "completed",
            InputJson = JsonSerializer.Serialize(promptRequest, SemanticKernelJson.Options),
            OutputJson = JsonSerializer.Serialize(new
            {
                promptAssembly.Prompt,
                promptAssembly.IncludedChunkIds,
                promptAssembly.EstimatedPromptTokens,
                promptAssembly.HumanReadableCitations
            }, SemanticKernelJson.Options),
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

    private static RetrievedChunk[] DeserializeRetrievedChunks(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("matches", out var matchesElement))
            {
                var matches = JsonSerializer.Deserialize<List<RetrievedChunkEnvelope>>(matchesElement.GetRawText(), SemanticKernelJson.Options);
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