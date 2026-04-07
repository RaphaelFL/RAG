using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence;

public sealed class FileSystemDocumentCatalog : IDocumentCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _sync = new();
    private readonly string _catalogFilePath;
    private Dictionary<Guid, DocumentCatalogEntry> _documents;

    public FileSystemDocumentCatalog(IOptions<LocalPersistenceOptions> options, IHostEnvironment environment)
    {
        var basePath = ResolveBasePath(options.Value.BasePath, environment.ContentRootPath);
        Directory.CreateDirectory(basePath);
        _catalogFilePath = Path.Combine(basePath, options.Value.DocumentCatalogFileName);
        _documents = Load();
    }

    public void Upsert(DocumentCatalogEntry entry)
    {
        lock (_sync)
        {
            _documents[entry.DocumentId] = SanitizeForStorage(entry);
            Persist();
        }
    }

    public DocumentCatalogEntry? Get(Guid documentId)
    {
        lock (_sync)
        {
            return _documents.TryGetValue(documentId, out var entry)
                ? Clone(entry)
                : null;
        }
    }

    public IReadOnlyCollection<DocumentCatalogEntry> Query(FileSearchFilterDto? filters)
    {
        lock (_sync)
        {
            IEnumerable<DocumentCatalogEntry> query = _documents.Values;

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
    }

    public DocumentCatalogEntry? FindByContentHash(Guid tenantId, string contentHash)
    {
        lock (_sync)
        {
            var match = _documents.Values.FirstOrDefault(document =>
                document.TenantId == tenantId &&
                string.Equals(document.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(document.Status, "Deleted", StringComparison.OrdinalIgnoreCase));

            return match is null ? null : Clone(match);
        }
    }

    private Dictionary<Guid, DocumentCatalogEntry> Load()
    {
        if (!File.Exists(_catalogFilePath))
        {
            return new Dictionary<Guid, DocumentCatalogEntry>();
        }

        try
        {
            var json = File.ReadAllText(_catalogFilePath);
            var documents = JsonSerializer.Deserialize<List<DocumentCatalogEntry>>(json, SerializerOptions) ?? new List<DocumentCatalogEntry>();
            return documents.ToDictionary(document => document.DocumentId, Clone);
        }
        catch
        {
            return new Dictionary<Guid, DocumentCatalogEntry>();
        }
    }

    private void Persist()
    {
        var tempFilePath = _catalogFilePath + ".tmp";
        var payload = JsonSerializer.Serialize(_documents.Values.OrderBy(document => document.DocumentId).ToList(), SerializerOptions);
        File.WriteAllText(tempFilePath, payload);
        File.Move(tempFilePath, _catalogFilePath, overwrite: true);
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

    private static DocumentCatalogEntry SanitizeForStorage(DocumentCatalogEntry source)
    {
        var sanitized = Clone(source);
        sanitized.IndexedChunkCount = sanitized.IndexedChunkCount > 0
            ? sanitized.IndexedChunkCount
            : sanitized.Chunks.Count;
        sanitized.Chunks = new List<DocumentChunkIndexDto>();
        return sanitized;
    }

    private static string ResolveBasePath(string configuredPath, string contentRootPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }
}