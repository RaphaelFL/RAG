namespace Chatbot.Application.Abstractions;

public class DocumentChunkIndexDto
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public int PageNumber { get; set; }
    public string? Section { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
