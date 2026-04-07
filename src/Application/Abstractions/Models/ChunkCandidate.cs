namespace Chatbot.Application.Abstractions;

public sealed class ChunkCandidate
{
    public string ChunkId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? EntitiesJson { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
