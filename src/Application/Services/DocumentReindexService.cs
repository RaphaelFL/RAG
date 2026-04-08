using Chatbot.Application.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Chatbot.Application.Services;

public sealed class DocumentReindexService : IDocumentReindexService
{
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IDocumentReindexDocumentResolver _documentReindexDocumentResolver;
    private readonly IDocumentReindexAccessGuard _documentReindexAccessGuard;
    private readonly IReindexJobScheduler _reindexJobScheduler;
    private readonly ILogger<DocumentReindexService> _logger;

    [ActivatorUtilitiesConstructor]
    public DocumentReindexService(
        IDocumentCatalog documentCatalog,
        IDocumentReindexDocumentResolver documentReindexDocumentResolver,
        IDocumentReindexAccessGuard documentReindexAccessGuard,
        IReindexJobScheduler reindexJobScheduler,
        ILogger<DocumentReindexService> logger)
    {
        _documentCatalog = documentCatalog;
        _documentReindexDocumentResolver = documentReindexDocumentResolver;
        _documentReindexAccessGuard = documentReindexAccessGuard;
        _reindexJobScheduler = reindexJobScheduler;
        _logger = logger;
    }

    public DocumentReindexService(
        IDocumentCatalog documentCatalog,
        IRequestContextAccessor requestContextAccessor,
        IBackgroundJobQueue backgroundJobQueue,
        IDocumentAuthorizationService documentAuthorizationService,
        ISecurityAuditLogger securityAuditLogger,
        ILogger<DocumentReindexService> logger)
        : this(
            documentCatalog,
            new DocumentReindexDocumentResolver(documentCatalog),
            new DocumentReindexAccessGuard(requestContextAccessor, documentAuthorizationService, securityAuditLogger),
            new ReindexJobScheduler(backgroundJobQueue),
            logger)
    {
    }

    public async Task<ReindexDocumentResponseDto> ReindexAsync(Guid documentId, bool fullReindex, CancellationToken ct)
    {
        _logger.LogInformation("Reindexing document {documentId}, full: {isFull}", documentId, fullReindex);

        var document = _documentReindexDocumentResolver.GetRequired(documentId);
        _documentReindexAccessGuard.EnsureAccess(document);

        var jobId = Guid.NewGuid();
        document.UpdatedAtUtc = DateTime.UtcNow;
        document.Status = "ReindexPending";
        document.LastJobId = jobId;
        _documentCatalog.Upsert(document);

        await _reindexJobScheduler.ScheduleAsync(jobId, documentId, fullReindex, null, ct);
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
        var documents = _documentReindexDocumentResolver.ResolveBulk(request, tenantId);

        foreach (var document in documents)
        {
            _documentReindexAccessGuard.EnsureAccess(document);
            document.UpdatedAtUtc = DateTime.UtcNow;
            document.Status = "ReindexPending";
            document.LastJobId = jobId;
            _documentCatalog.Upsert(document);
        }

        foreach (var document in documents)
        {
            await _reindexJobScheduler.ScheduleAsync(
                jobId,
                document.DocumentId,
                string.Equals(request.Mode, "full", StringComparison.OrdinalIgnoreCase),
                request.ForceEmbeddingModel,
                ct);
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
}