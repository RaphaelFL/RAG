using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

public sealed class InMemorySearchIndexGateway : ISearchIndexGateway
{
    private static readonly List<InMemoryIndexedChunk> Index = new();
    private readonly IDocumentCatalog _documentCatalog;
    private readonly InMemoryIndexedChunkFactory _chunkFactory = new();
    private readonly InMemorySearchFilterMatcher _filterMatcher = new();
    private readonly InMemorySearchScoreCalculator _scoreCalculator = new();

    public InMemorySearchIndexGateway(IDocumentCatalog documentCatalog)
    {
        _documentCatalog = documentCatalog;
    }

    public Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
    {
        lock (Index)
        {
            foreach (var chunk in chunks)
            {
                Index.RemoveAll(existing => existing.ChunkId == chunk.ChunkId);
                Index.Add(_chunkFactory.CreateIndexedChunk(chunk, 0.95));
            }
        }

        return Task.CompletedTask;
    }

    public Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct)
    {
        List<DocumentChunkIndexDto> chunks;
        lock (Index)
        {
            chunks = Index
                .Where(item => item.DocumentId == documentId)
                .OrderBy(item => item.GetChunkIndex())
                .ThenBy(item => item.ChunkId, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.ToDocumentChunk())
                .ToList();
        }

        if (chunks.Count > 0)
        {
            return Task.FromResult(chunks);
        }

        var legacyDocument = _documentCatalog.Get(documentId);
        var legacyChunks = legacyDocument?.Chunks
            .Select(_chunkFactory.Clone)
            .ToList()
            ?? new List<DocumentChunkIndexDto>();

        return Task.FromResult(legacyChunks);
    }

    public Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        List<InMemoryIndexedChunk> candidateResults;
        lock (Index)
        {
            candidateResults = Index.Where(result => _filterMatcher.Matches(result, filters)).ToList();
        }

        if (candidateResults.Count == 0)
        {
            candidateResults = BuildFallbackResults(query, filters);
        }

        var orderedResults = candidateResults
            .Select(result => new SearchResultDto
            {
                ChunkId = result.ChunkId,
                DocumentId = result.DocumentId,
                Content = result.Content,
                Metadata = result.Metadata,
                Score = _scoreCalculator.Calculate(query, queryEmbedding, result)
            })
            .OrderByDescending(result => result.Score)
            .Take(topK)
            .ToList();

        return Task.FromResult(orderedResults);
    }

    public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
    {
        lock (Index)
        {
            Index.RemoveAll(item => item.DocumentId == documentId);
        }

        return Task.CompletedTask;
    }

    private List<InMemoryIndexedChunk> BuildFallbackResults(string query, FileSearchFilterDto? filters)
    {
        var documents = _documentCatalog.Query(filters);
        if (documents.Count == 0)
        {
            return new List<InMemoryIndexedChunk>();
        }

        return documents
            .SelectMany(document => document.Chunks)
            .Select(chunk => _chunkFactory.CreateIndexedChunk(chunk, 0.9))
            .ToList();
    }
}
