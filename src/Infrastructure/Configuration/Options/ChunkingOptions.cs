namespace Chatbot.Infrastructure.Configuration;

public sealed class ChunkingOptions
{
    public int DenseChunkSize { get; set; } = 420;
    public int DenseOverlap { get; set; } = 48;
    public int NarrativeChunkSize { get; set; } = 900;
    public int NarrativeOverlap { get; set; } = 96;
    public int MinimumChunkCharacters { get; set; } = 120;
}
