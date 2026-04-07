using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chatbot.Domain.Entities;

namespace Chatbot.Application.Services;

public class IngestionService : IIngestionPipeline
{
    private readonly IBlobStorageGateway _blobGateway;
    private readonly IDocumentTextExtractor _documentTextExtractor;
    private readonly IMalwareScanner _malwareScanner;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ISearchIndexGateway _indexGateway;
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly IBackgroundJobQueue _backgroundJobQueue;
    private readonly IDocumentAuthorizationService _documentAuthorizationService;
    private readonly IPromptInjectionDetector _promptInjectionDetector;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        IBlobStorageGateway blobGateway,
        IDocumentTextExtractor documentTextExtractor,
        IMalwareScanner malwareScanner,
        IEmbeddingProvider embeddingProvider,
        ISearchIndexGateway indexGateway,
        IChunkingStrategy chunkingStrategy,
        IDocumentCatalog documentCatalog,
        IRequestContextAccessor requestContextAccessor,
        IBackgroundJobQueue backgroundJobQueue,
        IDocumentAuthorizationService documentAuthorizationService,
        IPromptInjectionDetector promptInjectionDetector,
        ISecurityAuditLogger securityAuditLogger,
        ILogger<IngestionService> logger)
    {
        _blobGateway = blobGateway;
        _documentTextExtractor = documentTextExtractor;
        _malwareScanner = malwareScanner;
        _embeddingProvider = embeddingProvider;
        _indexGateway = indexGateway;
        _chunkingStrategy = chunkingStrategy;
        _documentCatalog = documentCatalog;
        _requestContextAccessor = requestContextAccessor;
        _backgroundJobQueue = backgroundJobQueue;
        _documentAuthorizationService = documentAuthorizationService;
        _promptInjectionDetector = promptInjectionDetector;
        _securityAuditLogger = securityAuditLogger;
        _logger = logger;
    }

    public async Task<UploadDocumentResponseDto> IngestAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        _logger.LogInformation("Starting ingestion for document {documentId}", command.DocumentId);
        var payload = await ReadContentAsync(command.Content, ct);
        var rawHash = ComputeHash(payload);
        var duplicate = _documentCatalog.FindByContentHash(command.TenantId, rawHash);
        var existingFailedDocument = duplicate is not null && CanRetryFailedDuplicate(duplicate)
            ? duplicate
            : null;

        if (duplicate is not null && existingFailedDocument is null)
        {
            throw new DuplicateDocumentException($"A document with the same content already exists for this tenant: {duplicate.DocumentId}");
        }

        var jobId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var documentId = existingFailedDocument?.DocumentId ?? command.DocumentId;
        var createdAtUtc = existingFailedDocument?.CreatedAtUtc ?? timestamp;
        var version = existingFailedDocument?.Version ?? 1;
        var storagePath = $"documents/{command.TenantId}/{documentId}/raw-content";
        await _blobGateway.SaveAsync(new MemoryStream(payload, writable: false), storagePath, ct);

        var malwareResult = await _malwareScanner.ScanAsync(CloneCommand(command, payload), ct);
        if (!malwareResult.IsSafe)
        {
            var quarantinePath = malwareResult.RequiresQuarantine
                ? $"quarantine/{command.TenantId}/{documentId}/{command.FileName}"
                : null;

            if (quarantinePath is not null)
            {
                await _blobGateway.SaveAsync(new MemoryStream(payload, writable: false), quarantinePath, ct);
            }

            _documentCatalog.Upsert(new DocumentCatalogEntry
            {
                DocumentId = documentId,
                TenantId = command.TenantId,
                Title = string.IsNullOrWhiteSpace(command.DocumentTitle) ? command.FileName : command.DocumentTitle,
                OriginalFileName = command.FileName,
                ContentType = string.IsNullOrWhiteSpace(command.ContentType) ? "application/octet-stream" : command.ContentType,
                Source = command.Source,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = timestamp,
                Status = "Failed",
                Version = version,
                ContentHash = rawHash,
                Tags = command.Tags,
                Categories = command.Categories,
                Category = command.Category,
                ExternalId = command.ExternalId,
                AccessPolicy = command.AccessPolicy,
                StoragePath = storagePath,
                QuarantinePath = quarantinePath,
                LastJobId = jobId,
                IndexedChunkCount = 0,
                Chunks = new List<DocumentChunkIndexDto>()
            });

            throw new InvalidOperationException(malwareResult.Reason ?? "File rejected by malware scan.");
        }

        _documentCatalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = command.TenantId,
            Title = string.IsNullOrWhiteSpace(command.DocumentTitle) ? command.FileName : command.DocumentTitle,
            OriginalFileName = command.FileName,
            ContentType = string.IsNullOrWhiteSpace(command.ContentType) ? "application/octet-stream" : command.ContentType,
            Source = command.Source,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = timestamp,
            Status = "Queued",
            Version = version,
            ContentHash = rawHash,
            Tags = command.Tags,
            Categories = command.Categories,
            Category = command.Category,
            ExternalId = command.ExternalId,
            AccessPolicy = command.AccessPolicy,
            StoragePath = storagePath,
            LastJobId = jobId,
            IndexedChunkCount = existingFailedDocument?.IndexedChunkCount ?? 0,
            Chunks = new List<DocumentChunkIndexDto>()
        });

        await _backgroundJobQueue.EnqueueAsync(async (serviceProvider, jobCt) =>
        {
            var processor = serviceProvider.GetRequiredService<IIngestionJobProcessor>();
            await processor.ProcessIngestionAsync(new IngestionBackgroundJob
            {
                JobId = jobId,
                DocumentId = documentId,
                TenantId = command.TenantId,
                FileName = command.FileName,
                ContentType = command.ContentType,
                ContentLength = command.ContentLength,
                DocumentTitle = command.DocumentTitle,
                Category = command.Category,
                Tags = new List<string>(command.Tags),
                Categories = new List<string>(command.Categories),
                Source = command.Source,
                ExternalId = command.ExternalId,
                AccessPolicy = command.AccessPolicy,
                Payload = payload,
                RawHash = rawHash,
                StoragePath = storagePath
            }, jobCt);
        }, ct);
        ChatbotTelemetry.IngestionJobsQueued.Add(1);

        return new UploadDocumentResponseDto
        {
            DocumentId = documentId,
            Status = "Queued",
            IngestionJobId = jobId,
            TimestampUtc = timestamp,
            CreatedAtUtc = timestamp
        };
    }

    public async Task<ReindexDocumentResponseDto> ReindexAsync(Guid documentId, bool fullReindex, CancellationToken ct)
    {
        _logger.LogInformation("Reindexing document {documentId}, full: {isFull}", documentId, fullReindex);

        var document = _documentCatalog.Get(documentId);
        if (document is null)
        {
            throw new KeyNotFoundException($"Document {documentId} not found");
        }

        EnsureTenantAccess(document);

        var jobId = Guid.NewGuid();
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.Status = "ReindexPending";
        document.LastJobId = jobId;
        _documentCatalog.Upsert(document);

        await _backgroundJobQueue.EnqueueAsync(async (serviceProvider, jobCt) =>
        {
            var processor = serviceProvider.GetRequiredService<IIngestionJobProcessor>();
            await processor.ProcessReindexAsync(new ReindexBackgroundJob
            {
                JobId = jobId,
                DocumentId = documentId,
                FullReindex = fullReindex,
                ForceEmbeddingModel = null
            }, jobCt);
        }, ct);
        ChatbotTelemetry.ReindexJobsQueued.Add(1);

        return new ReindexDocumentResponseDto
        {
            DocumentId = documentId,
            Status = "ReindexPending",
            ChunksReindexed = 0,
            JobId = jobId
        };
    }

    public async Task<BulkReindexResponseDto> ReindexAsync(BulkReindexRequestDto request, Guid tenantId, CancellationToken ct)
    {
        var jobId = Guid.NewGuid();
        var documents = ResolveBulkReindexDocuments(request, tenantId);

        foreach (var document in documents)
        {
            EnsureTenantAccess(document);
            document.UpdatedAtUtc = DateTime.UtcNow;
            document.Status = "ReindexPending";
            document.LastJobId = jobId;
            _documentCatalog.Upsert(document);
        }

        foreach (var document in documents)
        {
            await _backgroundJobQueue.EnqueueAsync(async (serviceProvider, jobCt) =>
            {
                var processor = serviceProvider.GetRequiredService<IIngestionJobProcessor>();
                await processor.ProcessReindexAsync(new ReindexBackgroundJob
                {
                    JobId = jobId,
                    DocumentId = document.DocumentId,
                    FullReindex = string.Equals(request.Mode, "full", StringComparison.OrdinalIgnoreCase),
                    ForceEmbeddingModel = request.ForceEmbeddingModel
                }, jobCt);
            }, ct);
        }
        ChatbotTelemetry.ReindexJobsQueued.Add(documents.Count);

        return new BulkReindexResponseDto
        {
            Accepted = true,
            JobId = jobId,
            Mode = request.Mode,
            DocumentCount = documents.Count
        };
    }

    private List<DocumentCatalogEntry> ResolveBulkReindexDocuments(BulkReindexRequestDto request, Guid tenantId)
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
        var canAccess = HasTenantAccess(document);

        if (!canAccess)
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

    private DocumentDetailsDto MapDocumentDetails(DocumentCatalogEntry document)
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

    private static IngestDocumentCommand CloneCommand(IngestDocumentCommand command, byte[] payload)
    {
        return new IngestDocumentCommand
        {
            DocumentId = command.DocumentId,
            TenantId = command.TenantId,
            FileName = command.FileName,
            ContentType = command.ContentType,
            ContentLength = command.ContentLength,
            DocumentTitle = command.DocumentTitle,
            Category = command.Category,
            Tags = new List<string>(command.Tags),
            Categories = new List<string>(command.Categories),
            Source = command.Source,
            ExternalId = command.ExternalId,
            AccessPolicy = command.AccessPolicy,
            Content = new MemoryStream(payload, writable: false)
        };
    }

    private static async Task<byte[]> ReadContentAsync(Stream content, CancellationToken ct)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        return buffer.ToArray();
    }

    private static string ComputeHash(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content));
    }

    private static bool CanRetryFailedDuplicate(DocumentCatalogEntry document)
    {
        return string.Equals(document.Status, "Failed", StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveIndexedChunkCount(DocumentCatalogEntry document)
    {
        return document.IndexedChunkCount > 0
            ? document.IndexedChunkCount
            : document.Chunks.Count;
    }

}
