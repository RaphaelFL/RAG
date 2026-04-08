using System.Text.Json;

namespace Chatbot.Application.Services;

public sealed class PromptAssemblyAuditLogger : IPromptAssemblyAuditLogger
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IOperationalAuditWriter _operationalAuditWriter;

    public PromptAssemblyAuditLogger(IOperationalAuditWriter operationalAuditWriter)
    {
        _operationalAuditWriter = operationalAuditWriter;
    }

    public Task WriteAsync(PromptAssemblyRequest request, PromptAssemblyResult result, CancellationToken ct)
    {
        return _operationalAuditWriter.WritePromptAssemblyAsync(new PromptAssemblyRecord
        {
            PromptAssemblyId = Guid.NewGuid(),
            TenantId = request.TenantId,
            PromptTemplateId = request.AllowGeneralKnowledge ? "current-state-general" : "current-state-grounded",
            MaxPromptTokens = request.MaxPromptTokens,
            UsedPromptTokens = result.EstimatedPromptTokens,
            IncludedChunkIdsJson = JsonSerializer.Serialize(result.IncludedChunkIds, SerializerOptions),
            PromptBody = result.Prompt,
            CreatedAtUtc = DateTime.UtcNow
        }, ct);
    }
}