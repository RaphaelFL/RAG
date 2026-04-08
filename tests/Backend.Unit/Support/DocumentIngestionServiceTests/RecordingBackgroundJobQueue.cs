using Chatbot.Application.Abstractions;

namespace Backend.Unit.DocumentIngestionServiceTestsSupport;

internal sealed class RecordingBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Queue<BackgroundWorkItem> _items = new();

    public int EnqueueCount { get; private set; }

    public ValueTask EnqueueAsync(BackgroundWorkItem workItem, CancellationToken ct = default)
    {
        EnqueueCount++;
        _items.Enqueue(workItem);
        return ValueTask.CompletedTask;
    }

    public ValueTask<BackgroundWorkItem> DequeueAsync(CancellationToken ct)
    {
        return ValueTask.FromResult(_items.Dequeue());
    }
}