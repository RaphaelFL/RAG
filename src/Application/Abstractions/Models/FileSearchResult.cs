namespace Chatbot.Application.Abstractions;

public sealed class FileSearchResult
{
    public IReadOnlyCollection<RetrievedChunk> Matches { get; set; } = Array.Empty<RetrievedChunk>();
}
