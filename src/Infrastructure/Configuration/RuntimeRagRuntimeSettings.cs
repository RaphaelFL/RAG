using Chatbot.Application.Abstractions;
namespace Chatbot.Infrastructure.Configuration;

public sealed class RuntimeRagRuntimeSettings : IRagRuntimeSettings
{
    private readonly RagRuntimeSettingsStore _store;

    public RuntimeRagRuntimeSettings(RagRuntimeSettingsStore store)
    {
        _store = store;
    }

    public int DenseChunkSize => _store.GetSnapshot().DenseChunkSize;

    public int DenseOverlap => _store.GetSnapshot().DenseOverlap;

    public int NarrativeChunkSize => _store.GetSnapshot().NarrativeChunkSize;

    public int NarrativeOverlap => _store.GetSnapshot().NarrativeOverlap;

    public int MinimumChunkCharacters => _store.GetSnapshot().MinimumChunkCharacters;

    public int RetrievalCandidateMultiplier => _store.GetSnapshot().RetrievalCandidateMultiplier;

    public int RetrievalMaxCandidateCount => _store.GetSnapshot().RetrievalMaxCandidateCount;

    public int MaxContextChunks => _store.GetSnapshot().MaxContextChunks;

    public double MinimumRerankScore => _store.GetSnapshot().MinimumRerankScore;

    public double ExactMatchBoost => _store.GetSnapshot().ExactMatchBoost;

    public double TitleMatchBoost => _store.GetSnapshot().TitleMatchBoost;

    public double FilterMatchBoost => _store.GetSnapshot().FilterMatchBoost;

    public TimeSpan RetrievalCacheTtl => TimeSpan.FromSeconds(_store.GetSnapshot().RetrievalCacheTtlSeconds);

    public TimeSpan ChatCompletionCacheTtl => TimeSpan.FromSeconds(_store.GetSnapshot().ChatCompletionCacheTtlSeconds);

    public TimeSpan EmbeddingCacheTtl => TimeSpan.FromHours(_store.GetSnapshot().EmbeddingCacheTtlHours);
}