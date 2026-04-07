namespace Chatbot.Infrastructure.Agentic;

internal sealed class RetrievedChunkEnvelope
{
    public string? ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public double Score { get; set; }
    public string? Text { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}