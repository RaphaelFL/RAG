using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;

namespace Chatbot.Infrastructure.Providers;

internal static class GroundedChatPromptComposer
{
    public static string BuildSystemPrompt(ChatCompletionRequest request)
    {
        var groundedInstructions = request.AllowGeneralKnowledge
            ? "Responda em portugues do Brasil. Se houver contexto documental, priorize-o. Se nao houver, pode responder com conhecimento geral, deixando isso implicito na resposta."
            : "Responda em portugues do Brasil. Use apenas o contexto documental fornecido. Se o contexto for insuficiente, seja explicito e nao invente informacoes.";

        return $"Template: {request.Template.TemplateId} v{request.Template.Version}. {groundedInstructions}";
    }

    public static string BuildUserPrompt(ChatCompletionRequest request, int maxPromptContextTokens)
    {
        var context = BuildContextBlock(request.Message, request.RetrievedChunks, maxPromptContextTokens);
        return string.IsNullOrWhiteSpace(context)
            ? request.Message
            : $"Pergunta do usuario:\n{request.Message}\n\nContexto documental:\n{context}";
    }

    private static string BuildContextBlock(string message, IReadOnlyCollection<RetrievedChunkDto> chunks, int maxPromptContextTokens)
    {
        if (chunks.Count == 0)
        {
            return string.Empty;
        }

        var reservedTokens = EstimateTokens(message) + 256;
        var remainingBudget = Math.Max(0, maxPromptContextTokens - reservedTokens);
        if (remainingBudget <= 0)
        {
            return string.Empty;
        }

        var blocks = new List<string>(chunks.Count);
        var consumedTokens = 0;
        var sourceNumber = 1;

        foreach (var chunk in chunks)
        {
            var header = BuildChunkHeader(chunk, sourceNumber);
            var headerTokens = EstimateTokens(header);
            var contentBudget = remainingBudget - consumedTokens - headerTokens;
            if (contentBudget <= 24)
            {
                break;
            }

            var trimmedContent = TrimToTokenBudget(chunk.Content, contentBudget);
            if (string.IsNullOrWhiteSpace(trimmedContent))
            {
                continue;
            }

            var block = $"{header}\nConteudo: {trimmedContent}";
            var blockTokens = EstimateTokens(block);
            if (blockTokens > remainingBudget - consumedTokens)
            {
                break;
            }

            blocks.Add(block);
            consumedTokens += blockTokens;
            sourceNumber++;

            if (consumedTokens >= remainingBudget)
            {
                break;
            }
        }

        return string.Join("\n\n", blocks);
    }

    private static string BuildChunkHeader(RetrievedChunkDto chunk, int sourceNumber)
    {
        var location = chunk.PageNumber > 0
            ? chunk.EndPageNumber > chunk.PageNumber
                ? $"Paginas {chunk.PageNumber}-{chunk.EndPageNumber}"
                : $"Pagina {chunk.PageNumber}"
            : "Localizacao nao informada";

        var section = string.IsNullOrWhiteSpace(chunk.Section)
            ? string.Empty
            : $" | Secao: {chunk.Section}";

        return $"[Fonte {sourceNumber}] Documento: {chunk.DocumentTitle} | ChunkId: {chunk.ChunkId} | {location}{section}";
    }

    private static string TrimToTokenBudget(string content, int tokenBudget)
    {
        if (string.IsNullOrWhiteSpace(content) || tokenBudget <= 0)
        {
            return string.Empty;
        }

        if (EstimateTokens(content) <= tokenBudget)
        {
            return content;
        }

        var maxChars = Math.Max(80, (tokenBudget * 4) - 3);
        if (content.Length <= maxChars)
        {
            return content;
        }

        return content[..maxChars].TrimEnd() + "...";
    }

    private static int EstimateTokens(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? 0
            : (int)Math.Ceiling(text.Length / 4d);
    }
}