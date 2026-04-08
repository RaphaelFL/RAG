namespace Chatbot.Application.Abstractions;

public interface IIngestionCatalogEntryFactory
{
    DocumentCatalogEntry CreateFailed(IngestDocumentCommand command, IngestionPayloadContext context, Guid jobId, DateTime timestampUtc, string? quarantinePath);

    DocumentCatalogEntry CreateQueued(IngestDocumentCommand command, IngestionPayloadContext context, Guid jobId, DateTime timestampUtc);
}