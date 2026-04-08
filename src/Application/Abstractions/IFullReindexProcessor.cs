namespace Chatbot.Application.Abstractions;

public interface IFullReindexProcessor
{
    Task ProcessAsync(ReindexBackgroundJob job, CancellationToken ct);
}