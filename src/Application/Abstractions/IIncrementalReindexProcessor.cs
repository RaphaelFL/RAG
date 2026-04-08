namespace Chatbot.Application.Abstractions;

public interface IIncrementalReindexProcessor
{
    Task ProcessAsync(ReindexBackgroundJob job, CancellationToken ct);
}