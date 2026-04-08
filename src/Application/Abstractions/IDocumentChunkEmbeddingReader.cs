namespace Chatbot.Application.Abstractions;

public interface IDocumentChunkEmbeddingReader
{
    Task<DocumentChunkEmbeddingDto?> GetDocumentChunkEmbeddingAsync(Guid documentId, string chunkId, CancellationToken ct);
}