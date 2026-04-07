namespace Chatbot.Application.Contracts;

public sealed class GenerateEmbeddingItemResponseDtoV2
{
    public string ChunkId { get; set; } = string.Empty;
    public int VectorDimensions { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
}
