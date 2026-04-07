using Chatbot.Application.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Chatbot.Application.Services;

public sealed class DocumentReindexService : IDocumentReindexService
{
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly IBackgroundJobQueue _backgroundJobQueue;
    private readonly IDocumentAuthorizationService _documentAuthorizationService;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly ILogger<DocumentReindexService> _logger;

    public DocumentReindexService(
        IDocumentCatalog documentCatalog,
        IRequestContextAccessor requestContextAccessor,
        IBackgroundJobQueue backgroundJobQueue,
        IDocumentAuthorizationService documentAuthorizationService,
        ISecurityAuditLogger securityAuditLogger,
        ILogger<DocumentReindexService> logger)
    {
        _documentCatalog = documentCatalog;
        _requestContextAccessor = requestContextAccessor;
        _backgroundJobQueue = backgroundJobQueue;
        _documentAuthorizationService = documentAuthorizationService;
        _securityAuditLogger = securityAuditLogger;
        _logger = logger;
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

    private void EnsureTenantAccess(DocumentCatalogEntry document)
    {
        var canAccess = _requestContextAccessor.TenantId.HasValue && _documentAuthorizationService.CanAccess(
            document,
            _requestContextAccessor.TenantId,
            _requestContextAccessor.UserId,
            _requestContextAccessor.UserRole);

        if (!canAccess)
        {
            _securityAuditLogger.LogAccessDenied(_requestContextAccessor.UserId, $"document:{document.DocumentId}");
            throw new UnauthorizedAccessException("Document does not belong to the current tenant.");
        }
    }
}