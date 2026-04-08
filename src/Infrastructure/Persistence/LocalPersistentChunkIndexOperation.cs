using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class LocalPersistentChunkIndexOperation
{
    private readonly LocalPersistentSearchStorage _storage;

    public LocalPersistentChunkIndexOperation(LocalPersistentSearchStorage storage)
    {
        _storage = storage;
    }

    public Task ExecuteAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
    {
        _storage.Upsert(chunks);
        return Task.CompletedTask;
    }
}