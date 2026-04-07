namespace Chatbot.Infrastructure.Embeddings;

internal sealed class EmbeddingRuntimeRequest
{
    public string ModelName { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string ModelPath { get; set; } = string.Empty;
    public bool NormalizeVectors { get; set; }
    public int ExpectedDimensions { get; set; }
    public string[] Texts { get; set; } = Array.Empty<string>();
}