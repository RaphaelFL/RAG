namespace Chatbot.Application.Contracts;

public class DocumentChunkEmbeddingDto
{
    public Guid DocumentId { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public List<float> Values { get; set; } = new();
}
