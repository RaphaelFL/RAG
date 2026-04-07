namespace Chatbot.Application.Abstractions;

public sealed class EmbeddingBatchRequest
{
    public string EmbeddingModelName { get; set; } = string.Empty;
    public string EmbeddingModelVersion { get; set; } = string.Empty;
    public IReadOnlyCollection<EmbeddingInput> Inputs { get; set; } = Array.Empty<EmbeddingInput>();
}

public sealed class EmbeddingInput
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class EmbeddingEnvelope
{
    public string ChunkId { get; set; } = string.Empty;
    public string EmbeddingModelName { get; set; } = string.Empty;
    public string EmbeddingModelVersion { get; set; } = string.Empty;
    public int VectorDimensions { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
}