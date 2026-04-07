namespace Chatbot.Infrastructure.Configuration;

public sealed class SearchOptions
{
    public string IndexName { get; set; } = string.Empty;
    public string SemanticConfigurationName { get; set; } = string.Empty;
    public double HybridSearchWeight { get; set; }
    public bool SemanticRankingEnabled { get; set; }
    public int TopK { get; set; }
}

public sealed class ChunkingOptions
{
    public int DenseChunkSize { get; set; } = 420;
    public int DenseOverlap { get; set; } = 48;
    public int NarrativeChunkSize { get; set; } = 900;
    public int NarrativeOverlap { get; set; } = 96;
    public int MinimumChunkCharacters { get; set; } = 120;
}

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

public sealed class CacheOptions
{
    public int RetrievalTtlSeconds { get; set; } = 300;
    public int ChatCompletionTtlSeconds { get; set; } = 600;
    public int EmbeddingTtlHours { get; set; } = 24;
    public int MaxInMemoryEntries { get; set; } = 2000;
    public string InstancePrefix { get; set; } = "chatbot";
}