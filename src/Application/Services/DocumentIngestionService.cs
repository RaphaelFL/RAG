using Chatbot.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

namespace Chatbot.Application.Services;

public sealed class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IBlobStorageGateway _blobGateway;
    private readonly IMalwareScanner _malwareScanner;
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IBackgroundJobQueue _backgroundJobQueue;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        IBlobStorageGateway blobGateway,
        IMalwareScanner malwareScanner,
        IDocumentCatalog documentCatalog,
        IBackgroundJobQueue backgroundJobQueue,
        ILogger<DocumentIngestionService> logger)
    {
        _blobGateway = blobGateway;
        _malwareScanner = malwareScanner;
        _documentCatalog = documentCatalog;
        _backgroundJobQueue = backgroundJobQueue;
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
}