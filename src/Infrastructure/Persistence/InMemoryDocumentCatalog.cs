using System.Collections.Concurrent;
using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

public sealed class InMemoryDocumentCatalog : IDocumentCatalog
{
    private static readonly ConcurrentDictionary<Guid, DocumentCatalogEntry> Documents = new();

    public void Upsert(DocumentCatalogEntry entry)
    {
        Documents[entry.DocumentId] = entry;
    }

    public DocumentCatalogEntry? Get(Guid documentId)
    {
        Documents.TryGetValue(documentId, out var entry);
        return entry;
    }

    public IReadOnlyCollection<DocumentCatalogEntry> Query(FileSearchFilterDto? filters)
    {
        return Documents.Values
            .Where(document => !filters?.TenantId.HasValue ?? true || document.TenantId == filters!.TenantId!.Value)
            .Where(document => filters?.DocumentIds is not { Count: > 0 } || filters.DocumentIds.Contains(document.DocumentId))
            .Where(document => filters?.Tags is not { Count: > 0 } || filters.Tags.Any(tag => document.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            .Where(document => filters?.Categories is not { Count: > 0 } || filters.Categories.Any(category => document.Categories.Contains(category, StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    public DocumentCatalogEntry? FindByContentHash(Guid tenantId, string contentHash)
    {
        return Documents.Values.FirstOrDefault(document =>
            document.TenantId == tenantId &&
            string.Equals(document.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(document.Status, "Deleted", StringComparison.OrdinalIgnoreCase));
    }
}
