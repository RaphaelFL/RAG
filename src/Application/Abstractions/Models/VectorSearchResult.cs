namespace Chatbot.Application.Abstractions;

public sealed class VectorSearchResult
{
    public IReadOnlyCollection<RetrievedChunk> Chunks { get; set; } = Array.Empty<RetrievedChunk>();
    public string Strategy { get; set; } = string.Empty;
}
