namespace Chatbot.Application.Abstractions;

public sealed class RetrievalPlan
{
    public Guid TenantId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public bool UseHybridRetrieval { get; set; }
    public bool UseDenseRetrieval { get; set; } = true;
    public bool UseReranking { get; set; } = true;
    public int TopK { get; set; }
    public int MaxContextChunks { get; set; }
    public Dictionary<string, string[]> Filters { get; set; } = new();
}
