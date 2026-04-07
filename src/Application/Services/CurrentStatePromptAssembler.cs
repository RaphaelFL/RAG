using System.Text;
using System.Text.Json;
using Chatbot.Application.Configuration;
using Chatbot.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Chatbot.Application.Services;

public sealed class CurrentStatePromptAssembler : IPromptAssembler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IOperationalAuditStore _operationalAuditStore;

    public CurrentStatePromptAssembler(IOperationalAuditStore operationalAuditStore)
    {
        _operationalAuditStore = operationalAuditStore;
    }

    public async Task<PromptAssemblyResult> AssembleAsync(PromptAssemblyRequest request, CancellationToken ct)
    {
        var selectedChunks = request.Chunks
            .GroupBy(chunk => chunk.ChunkId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Score).First())
            .OrderByDescending(chunk => chunk.Score)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine(request.SystemInstructions);
        builder.AppendLine();
        builder.AppendLine("Regras:");
        builder.AppendLine(request.AllowGeneralKnowledge
            ? "- Use o contexto recuperado como fonte principal; so complemente com conhecimento geral se necessario e deixe isso claro."
            : "- Responda apenas com base no contexto recuperado; se nao houver evidencia suficiente, diga explicitamente que faltou evidencia.");
        builder.AppendLine("- Nao inclua hashes, ids tecnicos opacos ou lixo operacional na resposta.");
        builder.AppendLine("- Cite origem humana legivel ao usar evidencias.");
        builder.AppendLine();
        builder.AppendLine("Contexto recuperado:");

        foreach (var chunk in selectedChunks)
        {
            var sourceName = chunk.Metadata.TryGetValue("documentTitle", out var title) && !string.IsNullOrWhiteSpace(title)
                ? title
                : chunk.DocumentId.ToString();
            var section = chunk.Metadata.TryGetValue("section", out var sectionValue) ? sectionValue : string.Empty;
            var page = chunk.Metadata.TryGetValue("page", out var pageValue) ? pageValue : string.Empty;
            builder.AppendLine($"[{chunk.ChunkId}] Fonte: {sourceName}; Secao: {section}; Pagina: {page}");
            builder.AppendLine(chunk.Text);
            builder.AppendLine();
        }

        builder.AppendLine("Pergunta do usuario:");
        builder.AppendLine(request.UserQuestion);

        var result = new PromptAssemblyResult
        {
            Prompt = builder.ToString(),
            IncludedChunkIds = selectedChunks.Select(chunk => chunk.ChunkId).ToArray(),
            EstimatedPromptTokens = Math.Max(1, builder.Length / 4),
            HumanReadableCitations = selectedChunks.Select(chunk =>
            {
                var sourceName = chunk.Metadata.TryGetValue("documentTitle", out var title) && !string.IsNullOrWhiteSpace(title)
                    ? title
                    : chunk.DocumentId.ToString();
                return $"{sourceName} ({chunk.ChunkId})";
            }).ToArray()
        };

        await _operationalAuditStore.WritePromptAssemblyAsync(new PromptAssemblyRecord
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

        return result;
    }
}
