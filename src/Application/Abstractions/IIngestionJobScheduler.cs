namespace Chatbot.Application.Abstractions;

public interface IIngestionJobScheduler
{
    Task ScheduleAsync(IngestDocumentCommand command, IngestionPayloadContext context, Guid jobId, CancellationToken ct);
}