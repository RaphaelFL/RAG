namespace Chatbot.Application.Services;

public sealed class DocumentQueryService : IDocumentQueryService
{
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly IDocumentAuthorizationService _documentAuthorizationService;
    private readonly ISearchIndexGateway _indexGateway;
    private readonly ISecurityAuditLogger _securityAuditLogger;

    public DocumentQueryService(
        IDocumentCatalog documentCatalog,
        IRequestContextAccessor requestContextAccessor,
        IDocumentAuthorizationService documentAuthorizationService,
        ISearchIndexGateway indexGateway,
        ISecurityAuditLogger securityAuditLogger)
    {
        _documentCatalog = documentCatalog;
        _requestContextAccessor = requestContextAccessor;
        _documentAuthorizationService = documentAuthorizationService;
        _indexGateway = indexGateway;
        _securityAuditLogger = securityAuditLogger;
    }

    public Task<IReadOnlyList<DocumentDetailsDto>> ListDocumentsAsync(CancellationToken ct)
    {
        IReadOnlyList<DocumentDetailsDto> documents = _documentCatalog.Query(null)
            .Where(document => !string.Equals(document.Status, "Deleted", StringComparison.OrdinalIgnoreCase))
            .Where(HasTenantAccess)
            .OrderByDescending(document => document.UpdatedAtUtc)
            .ThenBy(document => document.Title, StringComparer.OrdinalIgnoreCase)
            .Select(MapDocumentDetails)
            .ToList();

        return Task.FromResult(documents);
    }

    public Task<DocumentDetailsDto?> GetDocumentAsync(Guid documentId, CancellationToken ct)
    {
        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            return Task.FromResult<DocumentDetailsDto?>(null);
        }

        EnsureTenantAccess(document);

        return Task.FromResult<DocumentDetailsDto?>(MapDocumentDetails(document));
    }

    public async Task<DocumentInspectionDto?> GetDocumentInspectionAsync(Guid documentId, string? search, int pageNumber, int pageSize, CancellationToken ct)
    {
        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            return null;
        }

        EnsureTenantAccess(document);

        var sanitizedPageNumber = Math.Max(1, pageNumber);
        var sanitizedPageSize = Math.Clamp(pageSize, 1, 100);
        var chunks = await _indexGateway.GetDocumentChunksAsync(documentId, ct);
        var orderedChunks = chunks
            .OrderBy(ResolveChunkIndex)
            .ThenBy(chunk => chunk.PageNumber)
            .ThenBy(chunk => chunk.ChunkId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filteredChunks = string.IsNullOrWhiteSpace(search)
            ? orderedChunks
            : orderedChunks.Where(chunk => ChunkMatchesSearch(chunk, search)).ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(filteredChunks.Count / (double)sanitizedPageSize));
        var resolvedPageNumber = Math.Min(sanitizedPageNumber, totalPages);
        var pagedChunks = filteredChunks
            .Skip((resolvedPageNumber - 1) * sanitizedPageSize)
            .Take(sanitizedPageSize)
            .Select(MapChunkInspection)
            .ToList();

        return new DocumentInspectionDto
        {
            Document = MapDocumentDetails(document),
            EmbeddedChunkCount = orderedChunks.Count(chunk => chunk.Embedding is { Length: > 0 }),
            TotalChunkCount = orderedChunks.Count,
            FilteredChunkCount = filteredChunks.Count,
            PageNumber = resolvedPageNumber,
            PageSize = sanitizedPageSize,
            TotalPages = totalPages,
            Chunks = pagedChunks
        };
    }

    public async Task<DocumentChunkEmbeddingDto?> GetDocumentChunkEmbeddingAsync(Guid documentId, string chunkId, CancellationToken ct)
    {
        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            return null;
        }

        EnsureTenantAccess(document);

        var chunk = (await _indexGateway.GetDocumentChunksAsync(documentId, ct))
            .FirstOrDefault(item => string.Equals(item.ChunkId, chunkId, StringComparison.OrdinalIgnoreCase));

        if (chunk?.Embedding is null || chunk.Embedding.Length == 0)
        {
            return null;
        }

        return new DocumentChunkEmbeddingDto
        {
            DocumentId = documentId,
            ChunkId = chunk.ChunkId,
            Dimensions = chunk.Embedding.Length,
            Values = chunk.Embedding.ToList()
        };
    }

    private void EnsureTenantAccess(DocumentCatalogEntry document)
    {
        if (!HasTenantAccess(document))
        {
            _securityAuditLogger.LogAccessDenied(_requestContextAccessor.UserId, $"document:{document.DocumentId}");
            throw new UnauthorizedAccessException("Document does not belong to the current tenant.");
        }
    }

    private bool HasTenantAccess(DocumentCatalogEntry document)
    {
        return _requestContextAccessor.TenantId.HasValue && _documentAuthorizationService.CanAccess(
            document,
            _requestContextAccessor.TenantId,
            _requestContextAccessor.UserId,
            _requestContextAccessor.UserRole);
    }

    private static DocumentDetailsDto MapDocumentDetails(DocumentCatalogEntry document)
    {
        return new DocumentDetailsDto
        {
            DocumentId = document.DocumentId,
            Title = document.Title,
            Status = document.Status,
            Version = document.Version,
            IndexedChunkCount = ResolveIndexedChunkCount(document),
            ContentType = document.ContentType,
            Source = document.Source,
            LastJobId = document.LastJobId,
            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc,
            Metadata = new DocumentMetadataDto
            {
                Category = document.Category,
                Tags = document.Tags,
                Categories = document.Categories,
                ExternalId = document.ExternalId,
                AccessPolicy = document.AccessPolicy
            }
        };
    }

    private static DocumentChunkInspectionDto MapChunkInspection(DocumentChunkIndexDto chunk)
    {
        var dimensions = chunk.Embedding?.Length ?? 0;
        return new DocumentChunkInspectionDto
        {
            ChunkId = chunk.ChunkId,
            ChunkIndex = ResolveChunkIndex(chunk),
            Content = chunk.Content,
            CharacterCount = chunk.Content.Length,
            PageNumber = chunk.PageNumber,
            EndPageNumber = ResolveMetadataInt(chunk.Metadata, "endPage"),
            Section = chunk.Section,
            Metadata = new Dictionary<string, string>(chunk.Metadata),
            Embedding = new DocumentEmbeddingInspectionDto
            {
                Exists = dimensions > 0,
                Dimensions = dimensions,
                Preview = chunk.Embedding?.Take(8).ToList() ?? new List<float>()
            }
        };
    }

    private static bool ChunkMatchesSearch(DocumentChunkIndexDto chunk, string search)
    {
        var normalizedSearch = search.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return true;
        }

        if (chunk.ChunkId.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
            || chunk.Content.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(chunk.Section) && chunk.Section.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return chunk.Metadata.Any(pair =>
            pair.Key.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
            || pair.Value.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
    }

    private static int ResolveChunkIndex(DocumentChunkIndexDto chunk)
    {
        return ResolveMetadataInt(chunk.Metadata, "chunkIndex") ?? int.MaxValue;
    }

    private static int? ResolveMetadataInt(Dictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, out var parsed)
            ? parsed
            : null;
    }

    private static int ResolveIndexedChunkCount(DocumentCatalogEntry document)
    {
        return document.IndexedChunkCount > 0
            ? document.IndexedChunkCount
            : document.Chunks.Count;
    }
}