using System.Collections.Concurrent;
using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

public sealed class InMemoryDocumentCatalog : IDocumentCatalog
{
    private static readonly ConcurrentDictionary<Guid, DocumentCatalogEntry> Documents = new();

    public void Upsert(DocumentCatalogEntry entry)
    {
        Documents[entry.DocumentId] = Clone(entry);
    }

    public DocumentCatalogEntry? Get(Guid documentId)
    {
        Documents.TryGetValue(documentId, out var entry);
        return entry is null ? null : Clone(entry);
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

        return query.Select(Clone).ToList();
    }

    public DocumentCatalogEntry? FindByContentHash(Guid tenantId, string contentHash)
    {
        return Documents.Values.FirstOrDefault(document =>
            document.TenantId == tenantId &&
            string.Equals(document.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(document.Status, "Deleted", StringComparison.OrdinalIgnoreCase));
    }

    private static DocumentCatalogEntry Clone(DocumentCatalogEntry source)
    {
        return new DocumentCatalogEntry
        {
            DocumentId = source.DocumentId,
            TenantId = source.TenantId,
            Title = source.Title,
            OriginalFileName = source.OriginalFileName,
            ContentType = source.ContentType,
            Status = source.Status,
            Version = source.Version,
            Source = source.Source,
            ContentHash = source.ContentHash,
            Category = source.Category,
            Tags = new List<string>(source.Tags),
            Categories = new List<string>(source.Categories),
            ExternalId = source.ExternalId,
            AccessPolicy = source.AccessPolicy,
            StoragePath = source.StoragePath,
            QuarantinePath = source.QuarantinePath,
            LastJobId = source.LastJobId,
            CreatedAtUtc = source.CreatedAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc,
            IndexedChunkCount = source.IndexedChunkCount,
            Chunks = source.Chunks.Select(chunk => new DocumentChunkIndexDto
            {
                ChunkId = chunk.ChunkId,
                DocumentId = chunk.DocumentId,
                Content = chunk.Content,
                Embedding = chunk.Embedding?.ToArray(),
                PageNumber = chunk.PageNumber,
                Section = chunk.Section,
                Metadata = new Dictionary<string, string>(chunk.Metadata)
            }).ToList()
        };
    }
}
