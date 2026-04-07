namespace Chatbot.Application.Abstractions;

public interface IDocumentQueryService
{
    Task<IReadOnlyList<DocumentDetailsDto>> ListDocumentsAsync(CancellationToken ct);
    Task<DocumentDetailsDto?> GetDocumentAsync(Guid documentId, CancellationToken ct);
    Task<DocumentInspectionDto?> GetDocumentInspectionAsync(Guid documentId, string? search, int pageNumber, int pageSize, CancellationToken ct);
    Task<DocumentChunkEmbeddingDto?> GetDocumentChunkEmbeddingAsync(Guid documentId, string chunkId, CancellationToken ct);
}