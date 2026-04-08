using Chatbot.Application.Abstractions;

namespace Chatbot.Application.Services;

public sealed class IngestionCatalogEntryFactory : IIngestionCatalogEntryFactory
{
    public DocumentCatalogEntry CreateFailed(IngestDocumentCommand command, IngestionPayloadContext context, Guid jobId, DateTime timestampUtc, string? quarantinePath)
    {
        var entry = CreateBaseEntry(command, context, jobId, timestampUtc);
        entry.Status = "Failed";
        entry.QuarantinePath = quarantinePath;
        return entry;
    }

    public DocumentCatalogEntry CreateQueued(IngestDocumentCommand command, IngestionPayloadContext context, Guid jobId, DateTime timestampUtc)
    {
        var entry = CreateBaseEntry(command, context, jobId, timestampUtc);
        entry.Status = "Queued";
        entry.IndexedChunkCount = context.IndexedChunkCount;
        return entry;
    }

    private static DocumentCatalogEntry CreateBaseEntry(IngestDocumentCommand command, IngestionPayloadContext context, Guid jobId, DateTime timestampUtc)
    {
        return new DocumentCatalogEntry
        {
            DocumentId = context.DocumentId,
            TenantId = command.TenantId,
            Title = string.IsNullOrWhiteSpace(command.DocumentTitle) ? command.FileName : command.DocumentTitle,
            OriginalFileName = command.FileName,
            ContentType = string.IsNullOrWhiteSpace(command.ContentType) ? "application/octet-stream" : command.ContentType,
            Source = command.Source,
            CreatedAtUtc = context.CreatedAtUtc,
            UpdatedAtUtc = timestampUtc,
            Version = context.Version,
            ContentHash = context.RawHash,
            Tags = command.Tags,
            Categories = command.Categories,
            Category = command.Category,
            ExternalId = command.ExternalId,
            AccessPolicy = command.AccessPolicy,
            StoragePath = context.StoragePath,
            LastJobId = jobId,
            IndexedChunkCount = 0,
            Chunks = new List<DocumentChunkIndexDto>()
        };
    }
}