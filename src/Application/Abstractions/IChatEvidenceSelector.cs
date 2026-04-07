namespace Chatbot.Application.Abstractions;

public interface IChatEvidenceSelector
{
    IReadOnlyList<RetrievedChunkDto> Select(string message, IReadOnlyCollection<RetrievedChunkDto> chunks, int maxContextChunks);
}