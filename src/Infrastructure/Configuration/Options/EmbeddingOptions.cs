namespace Chatbot.Infrastructure.Configuration;

public sealed class EmbeddingOptions
{
    public string Model { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public int BatchSize { get; set; }
}
