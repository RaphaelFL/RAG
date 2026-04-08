namespace Chatbot.Infrastructure.Persistence;

internal sealed class LocalPersistentDocumentDeleteOperation
{
    private readonly LocalPersistentSearchStorage _storage;

    public LocalPersistentDocumentDeleteOperation(LocalPersistentSearchStorage storage)
    {
        _storage = storage;
    }

    public Task ExecuteAsync(Guid documentId, CancellationToken ct)
    {
        _storage.DeleteDocument(documentId);
        return Task.CompletedTask;
    }
}