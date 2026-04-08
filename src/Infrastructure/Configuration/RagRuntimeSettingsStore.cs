using Chatbot.Application.Contracts;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Configuration;

public sealed class RagRuntimeSettingsStore
{
    private readonly object _sync = new();
    private RagRuntimeSettingsDto _settings;

    public RagRuntimeSettingsStore(
        IOptions<ChunkingOptions> chunkingOptions,
        IOptions<RetrievalOptimizationOptions> retrievalOptions,
        IOptions<CacheOptions> cacheOptions)
    {
        _settings = new RagRuntimeSettingsDto
        {
            DenseChunkSize = Math.Max(1, chunkingOptions.Value.DenseChunkSize),
            DenseOverlap = Math.Max(0, chunkingOptions.Value.DenseOverlap),
            NarrativeChunkSize = Math.Max(1, chunkingOptions.Value.NarrativeChunkSize),
            NarrativeOverlap = Math.Max(0, chunkingOptions.Value.NarrativeOverlap),
            MinimumChunkCharacters = Math.Max(1, chunkingOptions.Value.MinimumChunkCharacters),
            RetrievalCandidateMultiplier = Math.Max(1, retrievalOptions.Value.CandidateMultiplier),
            RetrievalMaxCandidateCount = Math.Max(1, retrievalOptions.Value.MaxCandidateCount),
            MaxContextChunks = Math.Max(1, retrievalOptions.Value.MaxContextChunks),
            MinimumRerankScore = retrievalOptions.Value.MinimumRerankScore,
            ExactMatchBoost = retrievalOptions.Value.ExactMatchBoost,
            TitleMatchBoost = retrievalOptions.Value.TitleMatchBoost,
            FilterMatchBoost = retrievalOptions.Value.FilterMatchBoost,
            RetrievalCacheTtlSeconds = Math.Max(1, cacheOptions.Value.RetrievalTtlSeconds),
            ChatCompletionCacheTtlSeconds = Math.Max(1, cacheOptions.Value.ChatCompletionTtlSeconds),
            EmbeddingCacheTtlHours = Math.Max(1, cacheOptions.Value.EmbeddingTtlHours)
        };
    }

    public RagRuntimeSettingsDto GetSnapshot()
    {
        lock (_sync)
        {
            return Clone(_settings);
        }
    }

    public RagRuntimeSettingsDto Update(UpdateRagRuntimeSettingsDto request)
    {
        lock (_sync)
        {
            _settings = new RagRuntimeSettingsDto
            {
                DenseChunkSize = Math.Max(1, request.DenseChunkSize),
                DenseOverlap = Math.Max(0, request.DenseOverlap),
                NarrativeChunkSize = Math.Max(1, request.NarrativeChunkSize),
                NarrativeOverlap = Math.Max(0, request.NarrativeOverlap),
                MinimumChunkCharacters = Math.Max(1, request.MinimumChunkCharacters),
                RetrievalCandidateMultiplier = Math.Max(1, request.RetrievalCandidateMultiplier),
                RetrievalMaxCandidateCount = Math.Max(1, request.RetrievalMaxCandidateCount),
                MaxContextChunks = Math.Max(1, request.MaxContextChunks),
                MinimumRerankScore = Math.Max(0, request.MinimumRerankScore),
                ExactMatchBoost = Math.Max(0, request.ExactMatchBoost),
                TitleMatchBoost = Math.Max(0, request.TitleMatchBoost),
                FilterMatchBoost = Math.Max(0, request.FilterMatchBoost),
                RetrievalCacheTtlSeconds = Math.Max(1, request.RetrievalCacheTtlSeconds),
                ChatCompletionCacheTtlSeconds = Math.Max(1, request.ChatCompletionCacheTtlSeconds),
                EmbeddingCacheTtlHours = Math.Max(1, request.EmbeddingCacheTtlHours)
            };

            return Clone(_settings);
        }
    }

    private static RagRuntimeSettingsDto Clone(RagRuntimeSettingsDto source)
    {
        return new RagRuntimeSettingsDto
        {
            DenseChunkSize = source.DenseChunkSize,
            DenseOverlap = source.DenseOverlap,
            NarrativeChunkSize = source.NarrativeChunkSize,
            NarrativeOverlap = source.NarrativeOverlap,
            MinimumChunkCharacters = source.MinimumChunkCharacters,
            RetrievalCandidateMultiplier = source.RetrievalCandidateMultiplier,
            RetrievalMaxCandidateCount = source.RetrievalMaxCandidateCount,
            MaxContextChunks = source.MaxContextChunks,
            MinimumRerankScore = source.MinimumRerankScore,
            ExactMatchBoost = source.ExactMatchBoost,
            TitleMatchBoost = source.TitleMatchBoost,
            FilterMatchBoost = source.FilterMatchBoost,
            RetrievalCacheTtlSeconds = source.RetrievalCacheTtlSeconds,
            ChatCompletionCacheTtlSeconds = source.ChatCompletionCacheTtlSeconds,
            EmbeddingCacheTtlHours = source.EmbeddingCacheTtlHours
        };
    }
}