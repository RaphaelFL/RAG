namespace Chatbot.Application.Abstractions;

public sealed class RetrievedContext
{
    public IReadOnlyCollection<RetrievedChunk> Chunks { get; set; } = Array.Empty<RetrievedChunk>();
    public string RetrievalStrategy { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
}
