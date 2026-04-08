namespace Chatbot.Application.Abstractions;

public interface IIngestionBackgroundJobHandler
{
    Task ProcessAsync(IngestionBackgroundJob job, CancellationToken ct);
}