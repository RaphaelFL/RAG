namespace Chatbot.Application.Abstractions;

public sealed class VectorChunkRecord
{
    public string ChunkId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public float[] Vector { get; set; } = Array.Empty<float>();
    public Dictionary<string, string> Metadata { get; set; } = new();
}
