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
        var message = request.RetrievedChunks.Count == 0
            ? $"Nao encontrei evidencia documental suficiente para grounding. Em um provider real, a resposta geral seria acionada para: {request.Message}"
            : $"Com base nos documentos recuperados, encontrei {request.RetrievedChunks.Count} evidencias relevantes. Os principais trechos usados foram: {string.Join(", ", request.RetrievedChunks.Take(2).Select(c => c.ChunkId))}. Consulte as citations para validar os detalhes da resposta.";

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

    private static int EstimateTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(1, Encoding.UTF8.GetByteCount(text) / 4);
    }
}