using Chatbot.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace Chatbot.Application.Services;

public sealed class DocumentQueryService : IDocumentQueryService
{
    private readonly IDocumentCatalog _documentCatalog;
    private readonly ISearchIndexGateway _indexGateway;
    private readonly IDocumentQueryAccessGuard _accessGuard;
    private readonly IDocumentInspectionBuilder _inspectionBuilder;

    [ActivatorUtilitiesConstructor]
    public DocumentQueryService(
        IDocumentCatalog documentCatalog,
        ISearchIndexGateway indexGateway,
        IDocumentQueryAccessGuard accessGuard,
        IDocumentInspectionBuilder inspectionBuilder)
    {
        _documentCatalog = documentCatalog;
        _indexGateway = indexGateway;
        _accessGuard = accessGuard;
        _inspectionBuilder = inspectionBuilder;
    }

    public DocumentQueryService(
        IDocumentCatalog documentCatalog,
        IRequestContextAccessor requestContextAccessor,
        IDocumentAuthorizationService documentAuthorizationService,
        ISearchIndexGateway indexGateway,
        ISecurityAuditLogger securityAuditLogger)
        : this(
            documentCatalog,
            indexGateway,
            new DocumentQueryAccessGuard(requestContextAccessor, documentAuthorizationService, securityAuditLogger),
            new DocumentInspectionBuilder())
    {
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

    public async Task<DocumentTextPreviewDto?> GetDocumentTextPreviewAsync(Guid documentId, CancellationToken ct)
    {
        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            return null;
        }

        _accessGuard.EnsureTenantAccess(document);

        var orderedChunks = OrderChunks(await _indexGateway.GetDocumentChunksAsync(documentId, ct));
        var content = BuildPreviewContent(orderedChunks);

        return new DocumentTextPreviewDto
        {
            DocumentId = document.DocumentId,
            Title = document.Title,
            OriginalFileName = string.IsNullOrWhiteSpace(document.OriginalFileName)
                ? document.Title
                : document.OriginalFileName,
            Content = content,
            CharacterCount = content.Length,
            ChunkCount = orderedChunks.Count
        };
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

    public Task<DocumentFileReference?> GetOriginalDocumentAsync(Guid documentId, CancellationToken ct)
    {
        var document = _documentCatalog.Get(documentId);
        if (document is null || string.IsNullOrWhiteSpace(document.StoragePath))
        {
            return Task.FromResult<DocumentFileReference?>(null);
        }

        _accessGuard.EnsureTenantAccess(document);

        return Task.FromResult<DocumentFileReference?>(new DocumentFileReference
        {
            DocumentId = document.DocumentId,
            OriginalFileName = string.IsNullOrWhiteSpace(document.OriginalFileName)
                ? document.Title
                : document.OriginalFileName,
            ContentType = string.IsNullOrWhiteSpace(document.ContentType)
                ? "application/octet-stream"
                : document.ContentType,
            StoragePath = document.StoragePath
        });
    }

    private static List<DocumentChunkIndexDto> OrderChunks(IReadOnlyCollection<DocumentChunkIndexDto> chunks)
    {
        return chunks
            .OrderBy(DocumentQueryMapper.ResolveChunkIndex)
            .ThenBy(chunk => chunk.PageNumber)
            .ThenBy(chunk => chunk.ChunkId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildPreviewContent(IReadOnlyList<DocumentChunkIndexDto> orderedChunks)
    {
        var builder = new StringBuilder();

        foreach (var chunk in orderedChunks)
        {
            var content = NormalizePreviewChunk(chunk.Content);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            if (builder.Length == 0)
            {
                builder.Append(content);
                continue;
            }

            AppendMergedChunk(builder, content);
        }

        return builder.ToString();
    }

    private static string NormalizePreviewChunk(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    private static void AppendMergedChunk(StringBuilder builder, string nextContent)
    {
        var current = builder.ToString();
        var overlapLength = FindOverlapLength(current, nextContent);

        if (overlapLength > 0)
        {
            builder.Append(nextContent[overlapLength..]);
            return;
        }

        builder.AppendLine();
        builder.AppendLine();
        builder.Append(nextContent);
    }

    private static int FindOverlapLength(string current, string nextContent)
    {
        var maxLength = Math.Min(Math.Min(current.Length, nextContent.Length), 512);
        var minimumOverlap = Math.Min(48, Math.Max(12, nextContent.Length / 6));

        for (var length = maxLength; length >= minimumOverlap; length--)
        {
            if (current[^length..].Equals(nextContent[..length], StringComparison.Ordinal))
            {
                return length;
            }
        }

        return 0;
    }
}