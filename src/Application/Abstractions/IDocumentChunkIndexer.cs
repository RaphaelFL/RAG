namespace Chatbot.Application.Abstractions;

public interface IDocumentChunkIndexer
{
    Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct);
}