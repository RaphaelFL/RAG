namespace Chatbot.Application.Abstractions;

public interface IDocumentCatalogReader
{
    Task<IReadOnlyList<DocumentDetailsDto>> ListDocumentsAsync(CancellationToken ct);
    Task<DocumentDetailsDto?> GetDocumentAsync(Guid documentId, CancellationToken ct);
}