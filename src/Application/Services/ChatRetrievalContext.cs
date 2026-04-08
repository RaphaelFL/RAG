using Chatbot.Application.Contracts;

namespace Chatbot.Application.Services;

internal sealed class ChatRetrievalContext
{
    public required RetrievalResultDto RetrievalResult { get; init; }

    public required IReadOnlyList<RetrievedChunkDto> EvidenceChunks { get; init; }

    public required List<CitationDto> Citations { get; init; }

    public required int MaxContextChunks { get; init; }

    public required bool AttemptedRetrieval { get; init; }

    public required bool AllowsGeneralKnowledge { get; init; }
}