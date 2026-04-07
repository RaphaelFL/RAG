namespace Chatbot.Application.Abstractions;

public sealed class EmbeddingEnvelope
{
    public string ChunkId { get; set; } = string.Empty;
    public string EmbeddingModelName { get; set; } = string.Empty;
    public string EmbeddingModelVersion { get; set; } = string.Empty;
    public int VectorDimensions { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
}
