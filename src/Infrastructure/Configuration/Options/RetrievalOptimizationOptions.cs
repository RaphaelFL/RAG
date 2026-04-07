namespace Chatbot.Infrastructure.Configuration;

public sealed class RetrievalOptimizationOptions
{
    public int CandidateMultiplier { get; set; } = 3;
    public int MaxCandidateCount { get; set; } = 24;
    public int MaxContextChunks { get; set; } = 4;
    public double MinimumRerankScore { get; set; } = 0.2;
    public double ExactMatchBoost { get; set; } = 0.18;
    public double TitleMatchBoost { get; set; } = 0.08;
    public double FilterMatchBoost { get; set; } = 0.05;
}
