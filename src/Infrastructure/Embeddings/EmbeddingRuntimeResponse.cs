namespace Chatbot.Infrastructure.Embeddings;

internal sealed class EmbeddingRuntimeResponse
{
    public string ModelName { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public List<float[]> Vectors { get; set; } = new();
}