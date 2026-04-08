namespace Chatbot.Application.Services;

public sealed class DocumentQueryService : IDocumentQueryService
{
    private readonly IDocumentCatalog _documentCatalog;
    private readonly ISearchIndexGateway _indexGateway;
    private readonly DocumentQueryAccessGuard _accessGuard;
    private readonly DocumentInspectionBuilder _inspectionBuilder = new();

    public DocumentQueryService(
        IDocumentCatalog documentCatalog,
        IRequestContextAccessor requestContextAccessor,
        IDocumentAuthorizationService documentAuthorizationService,
        ISearchIndexGateway indexGateway,
        ISecurityAuditLogger securityAuditLogger)
    {
        _documentCatalog = documentCatalog;
        _indexGateway = indexGateway;
        _accessGuard = new DocumentQueryAccessGuard(requestContextAccessor, documentAuthorizationService, securityAuditLogger);
    }

    public Task<IReadOnlyList<DocumentDetailsDto>> ListDocumentsAsync(CancellationToken ct)
    {
        IReadOnlyList<DocumentDetailsDto> documents = _documentCatalog.Query(null)
            .Where(document => !string.Equals(document.Status, "Deleted", StringComparison.OrdinalIgnoreCase))
            .Where(_accessGuard.HasTenantAccess)
            .OrderByDescending(document => document.UpdatedAtUtc)
            .ThenBy(document => document.Title, StringComparer.OrdinalIgnoreCase)
            .Select(DocumentQueryMapper.MapDocumentDetails)
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

        _accessGuard.EnsureTenantAccess(document);

        return Task.FromResult<DocumentDetailsDto?>(DocumentQueryMapper.MapDocumentDetails(document));
    }

    public async Task<DocumentInspectionDto?> GetDocumentInspectionAsync(Guid documentId, string? search, int pageNumber, int pageSize, CancellationToken ct)
    {
        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            return null;
        }

        _accessGuard.EnsureTenantAccess(document);
        var chunks = await _indexGateway.GetDocumentChunksAsync(documentId, ct);
        return _inspectionBuilder.Build(document, chunks, search, pageNumber, pageSize);
    }

    public async Task<DocumentChunkEmbeddingDto?> GetDocumentChunkEmbeddingAsync(Guid documentId, string chunkId, CancellationToken ct)
    {
        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            return null;
        }

        _accessGuard.EnsureTenantAccess(document);

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
}