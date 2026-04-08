using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class LocalPersistentHybridSearchOperation
{
    private readonly LocalPersistentSearchStorage _storage;
    private readonly LocalPersistentSearchFallbackSource _fallbackSource;
    private readonly LocalPersistentSearchFilter _filter;
    private readonly LocalPersistentSearchScoreCalculator _scoreCalculator;

    public LocalPersistentHybridSearchOperation(
        LocalPersistentSearchStorage storage,
        LocalPersistentSearchFallbackSource fallbackSource,
        LocalPersistentSearchFilter filter,
        LocalPersistentSearchScoreCalculator scoreCalculator)
    {
        _storage = storage;
        _fallbackSource = fallbackSource;
        _filter = filter;
        _scoreCalculator = scoreCalculator;
    }

    public Task<List<SearchResultDto>> ExecuteAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        var candidateResults = _storage.GetAll()
            .Where(result => _filter.Matches(result, filters))
            .ToList();

        if (candidateResults.Count == 0)
        {
            candidateResults = _fallbackSource.BuildFallbackResults(filters);
        }

        var orderedResults = candidateResults
            .Select(result => new SearchResultDto
            {
                ChunkId = result.ChunkId,
                DocumentId = result.DocumentId,
                Content = result.Content,
                Metadata = new Dictionary<string, string>(result.Metadata),
                Score = _scoreCalculator.Calculate(query, queryEmbedding, result)
            })
            .OrderByDescending(result => result.Score)
            .Take(topK)
            .ToList();

        return Task.FromResult(orderedResults);
    }
}