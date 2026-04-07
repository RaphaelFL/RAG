namespace Chatbot.Application.Contracts;

public class DocumentChunkInspectionDto
{
    public string ChunkId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int CharacterCount { get; set; }
    public int PageNumber { get; set; }
    public int? EndPageNumber { get; set; }
    public string? Section { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DocumentEmbeddingInspectionDto Embedding { get; set; } = new();
}
