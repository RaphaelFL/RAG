namespace Chatbot.Application.Abstractions;

public interface IDocumentChunkReader
{
    Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct);
}