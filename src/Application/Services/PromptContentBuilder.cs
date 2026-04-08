using System.Text;

namespace Chatbot.Application.Services;

public sealed class PromptContentBuilder : IPromptContentBuilder
{
    public PromptAssemblyResult Build(PromptAssemblyRequest request, IReadOnlyList<RetrievedChunk> selectedChunks)
    {
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
            var sourceName = ResolveSourceName(chunk);
            var section = chunk.Metadata.TryGetValue("section", out var sectionValue) ? sectionValue : string.Empty;
            var page = chunk.Metadata.TryGetValue("page", out var pageValue) ? pageValue : string.Empty;
            builder.AppendLine($"[{chunk.ChunkId}] Fonte: {sourceName}; Secao: {section}; Pagina: {page}");
            builder.AppendLine(chunk.Text);
            builder.AppendLine();
        }

        builder.AppendLine("Pergunta do usuario:");
        builder.AppendLine(request.UserQuestion);

        return new PromptAssemblyResult
        {
            Prompt = builder.ToString(),
            IncludedChunkIds = selectedChunks.Select(chunk => chunk.ChunkId).ToArray(),
            EstimatedPromptTokens = Math.Max(1, builder.Length / 4),
            HumanReadableCitations = selectedChunks
                .Select(chunk => $"{ResolveSourceName(chunk)} ({chunk.ChunkId})")
                .ToArray()
        };
    }

    private static string ResolveSourceName(RetrievedChunk chunk)
    {
        return chunk.Metadata.TryGetValue("documentTitle", out var title) && !string.IsNullOrWhiteSpace(title)
            ? title
            : chunk.DocumentId.ToString();
    }
}