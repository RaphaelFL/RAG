using System.Security.Cryptography;
using System.Text;
using Chatbot.Application.Observability;
using Polly;

namespace Chatbot.Application.Services;

public sealed class IngestionChunkEnricher : IIngestionChunkEnricher
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ResiliencePipeline _resiliencePipeline;

    public IngestionChunkEnricher(IEmbeddingProvider embeddingProvider, ResiliencePipeline resiliencePipeline)
    {
        _embeddingProvider = embeddingProvider;
        _resiliencePipeline = resiliencePipeline;
    }

    public async Task<int> EnrichAsync(List<DocumentChunkIndexDto> chunks, string? forceEmbeddingModel, bool forceRefresh, CancellationToken ct)
    {
        var updatedEmbeddings = 0;

        foreach (var chunk in chunks)
        {
            chunk.Metadata["contentHash"] = ComputeHash(chunk.Content);
            if (!forceRefresh && chunk.Embedding is { Length: > 0 } && string.IsNullOrWhiteSpace(forceEmbeddingModel))
            {
                ChatbotTelemetry.EmbeddingReuse.Add(1, new KeyValuePair<string, object?>("reuse.kind", "existing-chunk"));
                continue;
            }

            chunk.Embedding = await _resiliencePipeline.ExecuteAsync(async token =>
                await _embeddingProvider.CreateEmbeddingAsync(chunk.Content, forceEmbeddingModel, token), ct);
            chunk.Metadata["embeddingModel"] = string.IsNullOrWhiteSpace(forceEmbeddingModel) ? "default" : forceEmbeddingModel;
            updatedEmbeddings++;
        }

        return updatedEmbeddings;
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }
}