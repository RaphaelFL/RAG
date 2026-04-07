namespace Chatbot.Application.Abstractions;

public sealed class RetrievedChunk
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public double Score { get; set; }
    public string Text { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}
