namespace Chatbot.Application.Abstractions;

public interface IIngestionJobProcessor
{
    Task ProcessIngestionAsync(IngestionBackgroundJob job, CancellationToken ct);
    Task ProcessReindexAsync(ReindexBackgroundJob job, CancellationToken ct);
}