using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using Chatbot.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Xunit;

namespace Backend.Unit.ChatOrchestratorServiceTestsSupport;

internal sealed class FakeCitationAssembler : ICitationAssembler
{
    public List<CitationDto> Assemble(IReadOnlyCollection<RetrievedChunkDto> chunks, int maxCitations)
    {
        return chunks.Take(maxCitations).Select(chunk => new CitationDto
        {
            DocumentId = chunk.DocumentId,
            ChunkId = chunk.ChunkId,
            DocumentTitle = chunk.DocumentTitle,
            Snippet = chunk.Content,
            Score = chunk.Score
        }).ToList();
    }
}
