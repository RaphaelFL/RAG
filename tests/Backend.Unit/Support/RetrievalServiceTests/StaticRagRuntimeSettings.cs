using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit.RetrievalServiceTestsSupport;

internal sealed class StaticRagRuntimeSettings : IRagRuntimeSettings
{
    public int DenseChunkSize => 420;
    public int DenseOverlap => 48;
    public int NarrativeChunkSize => 900;
    public int NarrativeOverlap => 96;
    public int MinimumChunkCharacters => 120;
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
