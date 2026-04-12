using Chatbot.Application.Observability;

namespace Chatbot.Application.Services;

public sealed class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IIngestionContentStorage _ingestionContentStorage;
    private readonly IMalwareScanner _malwareScanner;
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IIngestionCatalogEntryFactory _ingestionCatalogEntryFactory;
    private readonly IIngestionJobScheduler _ingestionJobScheduler;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        IIngestionContentStorage ingestionContentStorage,
        IMalwareScanner malwareScanner,
        IDocumentCatalog documentCatalog,
        IIngestionCatalogEntryFactory ingestionCatalogEntryFactory,
        IIngestionJobScheduler ingestionJobScheduler,
        ILogger<DocumentIngestionService> logger)
    {
        _ingestionContentStorage = ingestionContentStorage;
        _malwareScanner = malwareScanner;
        _documentCatalog = documentCatalog;
        _ingestionCatalogEntryFactory = ingestionCatalogEntryFactory;
        _ingestionJobScheduler = ingestionJobScheduler;
        _logger = logger;
    }

    public async Task<UploadDocumentResponseDto> IngestAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        _logger.LogInformation("Starting ingestion for document {documentId}", command.DocumentId);
        var payloadContext = await _ingestionContentStorage.PrepareAsync(command, ct);
        var jobId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var malwareResult = await _malwareScanner.ScanAsync(CloneCommand(command, payloadContext.Payload), ct);
        if (!malwareResult.IsSafe)
        {
            var quarantinePath = await _ingestionContentStorage.SaveQuarantineAsync(command, payloadContext, malwareResult.RequiresQuarantine, ct);
            _documentCatalog.Upsert(_ingestionCatalogEntryFactory.CreateFailed(command, payloadContext, jobId, timestamp, quarantinePath));

            throw new InvalidOperationException(malwareResult.Reason ?? "File rejected by malware scan.");
        }

        _documentCatalog.Upsert(_ingestionCatalogEntryFactory.CreateQueued(command, payloadContext, jobId, timestamp));

        await _ingestionJobScheduler.ScheduleAsync(command, payloadContext, jobId, ct);
        ChatbotTelemetry.IngestionJobsQueued.Add(1);

        return new UploadDocumentResponseDto
        {
            DocumentId = payloadContext.DocumentId,
            Status = "Queued",
            IngestionJobId = jobId,
            TimestampUtc = timestamp,
            CreatedAtUtc = timestamp
        };
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
            ClientExtractedText = command.ClientExtractedText,
            ClientExtractedPages = command.ClientExtractedPages.Select(ClonePage).ToList(),
            Content = new MemoryStream(payload, writable: false)
        };
    }

    private static PageExtractionDto ClonePage(PageExtractionDto page)
    {
        return new PageExtractionDto
        {
            PageNumber = page.PageNumber,
            Text = page.Text,
            WorksheetName = page.WorksheetName,
            SlideNumber = page.SlideNumber,
            SectionTitle = page.SectionTitle,
            TableId = page.TableId,
            FormId = page.FormId,
            Metadata = new Dictionary<string, string>(page.Metadata, StringComparer.OrdinalIgnoreCase),
            Tables = page.Tables
        };
    }
}