using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class InMemoryIndexedChunk : SearchResultDto
{
    public float[]? Embedding { get; init; }

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
}