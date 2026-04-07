namespace Chatbot.Application.Abstractions;

public interface IIngestionChunkEnricher
{
    Task<int> EnrichAsync(List<DocumentChunkIndexDto> chunks, string? forceEmbeddingModel, bool forceRefresh, CancellationToken ct);
}