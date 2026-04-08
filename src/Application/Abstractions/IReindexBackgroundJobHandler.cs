namespace Chatbot.Application.Abstractions;

public interface IReindexBackgroundJobHandler
{
    Task ProcessAsync(ReindexBackgroundJob job, CancellationToken ct);
}