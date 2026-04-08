namespace Chatbot.Application.Abstractions;

public interface IDocumentReindexDocumentResolver
{
    DocumentCatalogEntry GetRequired(Guid documentId);

    IReadOnlyList<DocumentCatalogEntry> ResolveBulk(BulkReindexRequestDto request, Guid tenantId);
}