namespace Chatbot.Application.Contracts;

public class RagRuntimeSettingsDto
{
    public int DenseChunkSize { get; set; }
    public int DenseOverlap { get; set; }
    public int NarrativeChunkSize { get; set; }
    public int NarrativeOverlap { get; set; }
    public int MinimumChunkCharacters { get; set; }
    public int RetrievalCandidateMultiplier { get; set; }
    public int RetrievalMaxCandidateCount { get; set; }
    public int MaxContextChunks { get; set; }
    public double MinimumRerankScore { get; set; }
    public double ExactMatchBoost { get; set; }
    public double TitleMatchBoost { get; set; }
    public double FilterMatchBoost { get; set; }
    public int RetrievalCacheTtlSeconds { get; set; }
    public int ChatCompletionCacheTtlSeconds { get; set; }
    public int EmbeddingCacheTtlHours { get; set; }
}
