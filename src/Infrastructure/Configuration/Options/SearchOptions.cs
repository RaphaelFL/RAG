namespace Chatbot.Infrastructure.Configuration;

public sealed class SearchOptions
{
    public string IndexName { get; set; } = string.Empty;
    public string SemanticConfigurationName { get; set; } = string.Empty;
    public double HybridSearchWeight { get; set; }
    public bool SemanticRankingEnabled { get; set; }
    public int TopK { get; set; }
}
