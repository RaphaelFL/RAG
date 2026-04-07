namespace Chatbot.Infrastructure.Configuration;

public sealed class CacheOptions
{
    public int RetrievalTtlSeconds { get; set; } = 300;
    public int ChatCompletionTtlSeconds { get; set; } = 600;
    public int EmbeddingTtlHours { get; set; } = 24;
    public int MaxInMemoryEntries { get; set; } = 2000;
    public string InstancePrefix { get; set; } = "chatbot";
}
