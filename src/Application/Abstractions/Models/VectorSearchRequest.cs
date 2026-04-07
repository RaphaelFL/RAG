namespace Chatbot.Application.Abstractions;

public sealed class VectorSearchRequest
{
    public Guid TenantId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public float[]? QueryVector { get; set; }
    public int TopK { get; set; }
    public double ScoreThreshold { get; set; }
    public Dictionary<string, string[]> Filters { get; set; } = new();
}
