namespace Chatbot.Application.Contracts;

public sealed class RetrievalRequestDto
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 8;
    public bool UseHybridRetrieval { get; set; } = true;
    public bool UseReranking { get; set; } = true;
    public Dictionary<string, string[]> Filters { get; set; } = new();
}
