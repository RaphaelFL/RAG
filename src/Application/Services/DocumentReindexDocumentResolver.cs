using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;

namespace Chatbot.Application.Services;

internal sealed class DocumentReindexDocumentResolver : IDocumentReindexDocumentResolver
{
    private readonly IDocumentCatalog _documentCatalog;

    public DocumentReindexDocumentResolver(IDocumentCatalog documentCatalog)
    {
        _documentCatalog = documentCatalog;
    }

    public DocumentCatalogEntry GetRequired(Guid documentId)
    {
        return _documentCatalog.Get(documentId)
            ?? throw new KeyNotFoundException($"Document {documentId} not found");
    }

    public IReadOnlyList<DocumentCatalogEntry> ResolveBulk(BulkReindexRequestDto request, Guid tenantId)
    {
        if (request.IncludeAllTenantDocuments)
        {
            return _documentCatalog.Query(null)
                .Where(document => document.TenantId == tenantId)
                .Where(document => !string.Equals(document.Status, "Deleted", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return request.DocumentIds
            .Select(documentId => _documentCatalog.Get(documentId))
            .Where(document => document is not null)
            .Cast<DocumentCatalogEntry>()
            .ToList();
    }
}