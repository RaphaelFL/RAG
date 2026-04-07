namespace Chatbot.Application.Contracts;

public class UsageMetadataDto
{
    public string Model { get; set; } = "gpt-4.1";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public long LatencyMs { get; set; }
    public string RetrievalStrategy { get; set; } = "hybrid";
    public Dictionary<string, long> RuntimeMetrics { get; set; } = new();
}
