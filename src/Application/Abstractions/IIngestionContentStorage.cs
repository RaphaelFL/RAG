namespace Chatbot.Application.Abstractions;

public interface IIngestionContentStorage
{
    Task<IngestionPayloadContext> PrepareAsync(IngestDocumentCommand command, CancellationToken ct);

    Task<string?> SaveQuarantineAsync(IngestDocumentCommand command, IngestionPayloadContext context, bool requiresQuarantine, CancellationToken ct);
}