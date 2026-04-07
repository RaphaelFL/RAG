namespace Chatbot.Application.Abstractions;

public interface ISearchIndexGateway
{
    Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct);
    Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct);
    Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken ct);
}