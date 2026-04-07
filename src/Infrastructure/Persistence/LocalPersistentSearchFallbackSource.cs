using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class LocalPersistentSearchFallbackSource
{
    private readonly IDocumentCatalog _documentCatalog;

    public LocalPersistentSearchFallbackSource(IDocumentCatalog documentCatalog)
    {
        _documentCatalog = documentCatalog;
    }

    public List<DocumentChunkIndexDto> GetLegacyDocumentChunks(Guid documentId)
    {
        return _documentCatalog.Get(documentId)?.Chunks
            .Select(CloneChunk)
            .ToList()
            ?? new List<DocumentChunkIndexDto>();
    }

    public List<LocalPersistentIndexedChunk> BuildFallbackResults(FileSearchFilterDto? filters)
    {
        return _documentCatalog.Query(filters)
            .SelectMany(document => document.Chunks)
            .Select(chunk => LocalPersistentIndexedChunk.From(chunk, 0.9))
            .ToList();
    }

    private static DocumentChunkIndexDto CloneChunk(DocumentChunkIndexDto chunk)
    {
        return new DocumentChunkIndexDto
        {
            ChunkId = chunk.ChunkId,
            DocumentId = chunk.DocumentId,
            Content = chunk.Content,
            Embedding = chunk.Embedding?.ToArray(),
            PageNumber = chunk.PageNumber,
            Section = chunk.Section,
            Metadata = new Dictionary<string, string>(chunk.Metadata)
        };
    }
}