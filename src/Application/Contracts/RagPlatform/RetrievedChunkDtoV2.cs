namespace Chatbot.Application.Contracts;

public sealed class RetrievedChunkDtoV2
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
