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
        IEnumerable<DocumentCatalogEntry> query = Documents.Values;

        if (filters?.TenantId is Guid tenantId)
        {
            query = query.Where(document => document.TenantId == tenantId);
        }

        if (filters?.DocumentIds is { Count: > 0 })
        {
            query = query.Where(document => filters.DocumentIds.Contains(document.DocumentId));
        }

        if (filters?.Tags is { Count: > 0 })
        {
            query = query.Where(document => filters.Tags.Any(tag => document.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
        }

        if (filters?.Categories is { Count: > 0 })
        {
            query = query.Where(document =>
                filters.Categories.Any(category => document.Categories.Contains(category, StringComparer.OrdinalIgnoreCase)) ||
                filters.Categories.Any(category => string.Equals(category, document.Category, StringComparison.OrdinalIgnoreCase)));
        }

        if (filters?.ContentTypes is { Count: > 0 })
        {
            query = query.Where(document => filters.ContentTypes.Any(contentType => string.Equals(contentType, document.ContentType, StringComparison.OrdinalIgnoreCase)));
        }

        if (filters?.Sources is { Count: > 0 })
        {
            query = query.Where(document => filters.Sources.Any(source => string.Equals(source, document.Source, StringComparison.OrdinalIgnoreCase)));
        }

        return query.ToList();
    }

    public DocumentCatalogEntry? FindByContentHash(Guid tenantId, string contentHash)
    {
        return Documents.Values.FirstOrDefault(document =>
            document.TenantId == tenantId &&
            string.Equals(document.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(document.Status, "Deleted", StringComparison.OrdinalIgnoreCase));
    }
}
