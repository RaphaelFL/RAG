using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class LocalPersistentChunkReadOperation
{
    private readonly LocalPersistentSearchStorage _storage;
    private readonly LocalPersistentSearchFallbackSource _fallbackSource;

    public LocalPersistentChunkReadOperation(LocalPersistentSearchStorage storage, LocalPersistentSearchFallbackSource fallbackSource)
    {
        _storage = storage;
        _fallbackSource = fallbackSource;
    }

    public Task<List<DocumentChunkIndexDto>> ExecuteAsync(Guid documentId, CancellationToken ct)
    {
        var chunks = _storage.GetDocumentChunks(documentId);
        return Task.FromResult(chunks.Count > 0
            ? chunks
            : _fallbackSource.GetLegacyDocumentChunks(documentId));
    }
}