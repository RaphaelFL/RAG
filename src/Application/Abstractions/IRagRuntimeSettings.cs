namespace Chatbot.Application.Abstractions;

public interface IRagRuntimeSettings
{
    int DenseChunkSize { get; }
    int DenseOverlap { get; }
    int NarrativeChunkSize { get; }
    int NarrativeOverlap { get; }
    int MinimumChunkCharacters { get; }
    int RetrievalCandidateMultiplier { get; }
    int RetrievalMaxCandidateCount { get; }
    int MaxContextChunks { get; }
    double MinimumRerankScore { get; }
    double ExactMatchBoost { get; }
    double TitleMatchBoost { get; }
    double FilterMatchBoost { get; }
    TimeSpan RetrievalCacheTtl { get; }
    TimeSpan ChatCompletionCacheTtl { get; }
    TimeSpan EmbeddingCacheTtl { get; }
}