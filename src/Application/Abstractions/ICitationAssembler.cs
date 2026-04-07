namespace Chatbot.Application.Abstractions;

public interface ICitationAssembler
{
    List<CitationDto> Assemble(IReadOnlyCollection<RetrievedChunkDto> chunks, int maxCitations);
}