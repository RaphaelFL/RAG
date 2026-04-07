namespace Chatbot.Application.Abstractions;

public interface IBackgroundJobQueue
{
    ValueTask EnqueueAsync(BackgroundWorkItem workItem, CancellationToken ct = default);
    ValueTask<BackgroundWorkItem> DequeueAsync(CancellationToken ct);
}