namespace Chatbot.Application.Contracts;

public sealed class GenerateEmbeddingsRequestDtoV2
{
    public string? EmbeddingModelName { get; set; }
    public string? EmbeddingModelVersion { get; set; }
    public List<GenerateEmbeddingItemRequestDtoV2> Items { get; set; } = new();
}

public sealed class GenerateEmbeddingItemRequestDtoV2
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class GenerateEmbeddingsResponseDtoV2
{
    public string ModelName { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public List<GenerateEmbeddingItemResponseDtoV2> Items { get; set; } = new();
}

public sealed class GenerateEmbeddingItemResponseDtoV2
{
    public string ChunkId { get; set; } = string.Empty;
    public int VectorDimensions { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
}