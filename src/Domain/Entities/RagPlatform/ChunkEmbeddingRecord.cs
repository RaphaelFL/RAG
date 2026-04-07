namespace Chatbot.Domain.Entities;

public sealed class ChunkEmbeddingRecord
{
    public Guid ChunkEmbeddingId { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string EmbeddingModelName { get; set; } = string.Empty;
    public string EmbeddingModelVersion { get; set; } = string.Empty;
    public int VectorDimensions { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
    public DateTime CreatedAtUtc { get; set; }
}
