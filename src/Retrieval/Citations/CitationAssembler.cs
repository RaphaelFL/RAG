using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;

namespace Chatbot.Retrieval.Citations;

public sealed class CitationAssembler : ICitationAssembler
{
    public List<CitationDto> Assemble(IReadOnlyCollection<RetrievedChunkDto> chunks, int maxCitations)
    {
        return chunks
            .Take(Math.Max(0, maxCitations))
            .Select(chunk => new CitationDto
            {
                DocumentId = chunk.DocumentId,
                DocumentTitle = chunk.DocumentTitle,
                ChunkId = chunk.ChunkId,
                Snippet = chunk.Content[..Math.Min(200, chunk.Content.Length)],
                Score = chunk.Score,
                Location = new LocationDto
                {
                    Page = chunk.PageNumber,
                    Section = chunk.Section
                }
            })
            .ToList();
    }
}
