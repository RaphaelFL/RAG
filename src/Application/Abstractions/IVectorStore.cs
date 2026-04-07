namespace Chatbot.Application.Abstractions;

public interface IVectorStore
{
    Task UpsertAsync(VectorUpsertRequest request, CancellationToken ct);
    Task<VectorSearchResult> SearchAsync(VectorSearchRequest request, CancellationToken ct);
    Task DeleteDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct);
}