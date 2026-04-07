namespace Chatbot.Application.Abstractions;

public interface IEmbeddingGenerationService
{
    Task<IReadOnlyCollection<EmbeddingEnvelope>> GenerateBatchAsync(EmbeddingBatchRequest request, CancellationToken ct);
}