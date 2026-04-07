namespace Chatbot.Application.Abstractions;

public interface IDocumentCatalog
{
    void Upsert(DocumentCatalogEntry entry);
    DocumentCatalogEntry? Get(Guid documentId);
    IReadOnlyCollection<DocumentCatalogEntry> Query(FileSearchFilterDto? filters);
    DocumentCatalogEntry? FindByContentHash(Guid tenantId, string contentHash);
}