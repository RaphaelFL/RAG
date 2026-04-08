using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class InMemoryIndexedChunkFactory
{
    public InMemoryIndexedChunk CreateIndexedChunk(DocumentChunkIndexDto chunk, double score)
    {
        return new InMemoryIndexedChunk
        {
            ChunkId = chunk.ChunkId,
            DocumentId = chunk.DocumentId,
            Content = chunk.Content,
            Score = score,
            Metadata = new Dictionary<string, string>(chunk.Metadata),
            Embedding = chunk.Embedding
        };
    }

    public DocumentChunkIndexDto Clone(DocumentChunkIndexDto chunk)
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