namespace Chatbot.Domain.Entities;

public class DocumentChunk
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public int PageNumber { get; set; }
    public string? Section { get; set; }
    public int Offset { get; set; }
    public int Length { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
