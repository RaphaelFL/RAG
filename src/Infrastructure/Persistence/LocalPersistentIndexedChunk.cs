using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class LocalPersistentIndexedChunk
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public float[]? Embedding { get; set; }

    public DocumentChunkIndexDto ToDocumentChunk()
    {
        return new DocumentChunkIndexDto
        {
            ChunkId = ChunkId,
            DocumentId = DocumentId,
            Content = Content,
            Embedding = Embedding?.ToArray(),
            PageNumber = GetPageNumber(),
            Section = Metadata.TryGetValue("section", out var section) ? section : null,
            Metadata = new Dictionary<string, string>(Metadata)
        };
    }

    public int GetChunkIndex()
    {
        return Metadata.TryGetValue("chunkIndex", out var rawChunkIndex) && int.TryParse(rawChunkIndex, out var chunkIndex)
            ? chunkIndex
            : 0;
    }

    private int GetPageNumber()
    {
        return Metadata.TryGetValue("startPage", out var rawStartPage) && int.TryParse(rawStartPage, out var startPage)
            ? startPage
            : Metadata.TryGetValue("page", out var rawPage) && int.TryParse(rawPage, out var page)
            ? page
            : 0;
    }

    public static LocalPersistentIndexedChunk From(DocumentChunkIndexDto chunk, double score = 0.95)
    {
        return new LocalPersistentIndexedChunk
        {
            ChunkId = chunk.ChunkId,
            DocumentId = chunk.DocumentId,
            Content = chunk.Content,
            Score = score,
            Metadata = new Dictionary<string, string>(chunk.Metadata),
            Embedding = chunk.Embedding?.ToArray()
        };
    }

    public static LocalPersistentIndexedChunk Clone(LocalPersistentIndexedChunk source)
    {
        return new LocalPersistentIndexedChunk
        {
            ChunkId = source.ChunkId,
            DocumentId = source.DocumentId,
            Content = source.Content,
            Score = source.Score,
            Metadata = new Dictionary<string, string>(source.Metadata),
            Embedding = source.Embedding?.ToArray()
        };
    }
}