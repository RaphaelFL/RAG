using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence;

public sealed class LocalPersistentSearchIndexGateway : ISearchIndexGateway
{
    private readonly LocalPersistentSearchStorage _storage;
    private readonly LocalPersistentSearchFallbackSource _fallbackSource;
    private readonly LocalPersistentSearchFilter _filter;
    private readonly LocalPersistentSearchScoreCalculator _scoreCalculator;

    public LocalPersistentSearchIndexGateway(IDocumentCatalog documentCatalog, IOptions<LocalPersistenceOptions> options, Microsoft.Extensions.Hosting.IHostEnvironment environment)
        : this(
            new LocalPersistentSearchStorage(options, environment),
            new LocalPersistentSearchFallbackSource(documentCatalog),
            new LocalPersistentSearchFilter(),
            new LocalPersistentSearchScoreCalculator())
    {
    }

    internal LocalPersistentSearchIndexGateway(
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

    public Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
    {
        _storage.Upsert(chunks);
        return Task.CompletedTask;
    }

    public Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct)
    {
        var chunks = _storage.GetDocumentChunks(documentId);

        if (chunks.Count > 0)
        {
            return Task.FromResult(chunks);
        }

        return Task.FromResult(_fallbackSource.GetLegacyDocumentChunks(documentId));
    }

    public Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
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

    public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
    {
        _storage.DeleteDocument(documentId);
        return Task.CompletedTask;
    }
}