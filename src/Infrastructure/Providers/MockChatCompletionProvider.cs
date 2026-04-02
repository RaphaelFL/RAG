using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Providers;

public sealed class MockChatCompletionProvider : IChatCompletionProvider
{
    private readonly ChatModelOptions _options;

    public MockChatCompletionProvider(IOptions<ChatModelOptions> options)
    {
        _options = options.Value;
    }

    public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        var message = BuildMessage(request);

        var estimatedPromptTokens = EstimateTokens(request.Message) + request.RetrievedChunks.Sum(chunk => EstimateTokens(chunk.Content));
        var estimatedCompletionTokens = EstimateTokens(message);

        return Task.FromResult(new ChatCompletionResult
        {
            Message = message,
            Model = _options.Model,
            PromptTokens = estimatedPromptTokens,
            CompletionTokens = estimatedCompletionTokens,
            TotalTokens = estimatedPromptTokens + estimatedCompletionTokens
        });
    }

    private static string BuildMessage(ChatCompletionRequest request)
    {
        if (request.RetrievedChunks.Count == 0)
        {
            return request.AllowGeneralKnowledge
                ? $"Conhecimento geral esta habilitado, mas este ambiente esta usando o provider mock. Nao houve evidencia documental recuperada para a pergunta '{request.Message}'."
                : "Nao encontrei evidencia documental suficiente para responder com seguranca a partir da base indexada.";
        }

        var highlights = request.RetrievedChunks
            .Select(chunk => ExtractBestExcerpt(chunk.Content, request.Message))
            .Where(excerpt => !string.IsNullOrWhiteSpace(excerpt))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        var evidenceSummary = highlights.Count == 0
            ? "Encontrei evidencias documentais relevantes, mas sem trechos legiveis suficientes para resumir melhor no provider mock."
            : string.Join(" ", highlights.Select((excerpt, index) => index == 0 ? excerpt : $"Complementando, {excerpt}"));

        if (!request.AllowGeneralKnowledge)
        {
            return evidenceSummary;
        }

        return $"{evidenceSummary} Se a evidencia nao cobrir toda a pergunta, o modo de conhecimento geral pode complementar em um provider completo.";
    }

    private static string ExtractBestExcerpt(string content, string question)
    {
        var sentences = SplitSentences(content);
        if (sentences.Count == 0)
        {
            return string.Empty;
        }

        var questionTokens = Tokenize(question);
        var bestSentence = sentences
            .Select(sentence => new
            {
                Sentence = sentence,
                Score = ScoreSentence(sentence, questionTokens)
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Sentence.Length)
            .First();

        return bestSentence.Sentence;
    }

    private static List<string> SplitSentences(string content)
    {
        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(sentence => string.Join(' ', sentence.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .Where(sentence => sentence.Length >= 12)
            .Where(sentence => !PdfTextExtraction.LooksLikeArtifactText(sentence))
            .Take(8)
            .ToList();
    }

    private static int ScoreSentence(string sentence, HashSet<string> questionTokens)
    {
        if (questionTokens.Count == 0)
        {
            return sentence.Length;
        }

        var sentenceTokens = Tokenize(sentence);
        var overlap = sentenceTokens.Count(token => questionTokens.Contains(token));
        return overlap * 100 + sentence.Length;
    }

    private static HashSet<string> Tokenize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static int EstimateTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(1, Encoding.UTF8.GetByteCount(text) / 4);
    }
}