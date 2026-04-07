using Chatbot.Application.Abstractions;
using Chatbot.Ingestion.Chunking;
using FluentAssertions;
using Xunit;

namespace Backend.Unit.SlidingWindowChunkingStrategyTestsSupport;

internal sealed class TestRagRuntimeSettings : IRagRuntimeSettings
{
    public int DenseChunkSize => 180;
    public int DenseOverlap => 72;
    public int NarrativeChunkSize => 180;
    public int NarrativeOverlap => 72;
    public int MinimumChunkCharacters => 40;
    public int RetrievalCandidateMultiplier => 3;
    public int RetrievalMaxCandidateCount => 24;
    public int MaxContextChunks => 4;
    public double MinimumRerankScore => 0.1;
    public double ExactMatchBoost => 0.18;
    public double TitleMatchBoost => 0.08;
    public double FilterMatchBoost => 0.05;
    public TimeSpan RetrievalCacheTtl => TimeSpan.FromMinutes(5);
    public TimeSpan ChatCompletionCacheTtl => TimeSpan.FromMinutes(10);
    public TimeSpan EmbeddingCacheTtl => TimeSpan.FromHours(24);
}
