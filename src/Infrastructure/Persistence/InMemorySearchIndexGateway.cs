using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

public sealed class InMemorySearchIndexGateway : ISearchIndexGateway
{
    private static readonly List<IndexedChunk> Index = new();
    private readonly IDocumentCatalog _documentCatalog;

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
                Index.Add(new IndexedChunk
                {
                    ChunkId = chunk.ChunkId,
                    DocumentId = chunk.DocumentId,
                    Content = chunk.Content,
                    Score = 0.95,
                    Metadata = new Dictionary<string, string>(chunk.Metadata),
                    Embedding = chunk.Embedding
                });
            }
        }

        return Task.CompletedTask;
    }

    public Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        List<IndexedChunk> candidateResults;
        lock (Index)
        {
            candidateResults = Index.Where(result => MatchesFilters(result, filters)).ToList();
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
                Score = CalculateScore(query, queryEmbedding, result)
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

    private List<IndexedChunk> BuildFallbackResults(string query, FileSearchFilterDto? filters)
    {
        var documents = _documentCatalog.Query(filters);
        if (documents.Count == 0)
        {
            return new List<IndexedChunk>();
        }

        return documents
            .SelectMany(document => document.Chunks)
            .Select(chunk => new IndexedChunk
            {
                ChunkId = chunk.ChunkId,
                DocumentId = chunk.DocumentId,
                Content = chunk.Content,
                Score = 0.9,
                Metadata = chunk.Metadata,
                Embedding = chunk.Embedding
            })
            .ToList();
    }

    private static bool MatchesFilters(IndexedChunk result, FileSearchFilterDto? filters)
    {
        if (filters is null)
        {
            return true;
        }

        if (filters.DocumentIds is { Count: > 0 } && !filters.DocumentIds.Contains(result.DocumentId))
        {
            return false;
        }

        if (filters.TenantId.HasValue && result.Metadata.TryGetValue("tenantId", out var tenantId) && tenantId != filters.TenantId.Value.ToString())
        {
            return false;
        }

        if (filters.Tags is { Count: > 0 })
        {
            var tags = result.Metadata.TryGetValue("tags", out var tagString)
                ? tagString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>();

            if (!filters.Tags.Any(tag => tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        if (filters.Categories is { Count: > 0 })
        {
            var categories = result.Metadata.TryGetValue("categories", out var categoriesString)
                ? categoriesString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>();

            if (!filters.Categories.Any(category => categories.Contains(category, StringComparer.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        if (filters.ContentTypes is { Count: > 0 })
        {
            var contentType = result.Metadata.TryGetValue("contentType", out var contentTypeValue)
                ? contentTypeValue
                : string.Empty;

            if (!filters.ContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (filters.Sources is { Count: > 0 })
        {
            var source = result.Metadata.TryGetValue("source", out var sourceValue)
                ? sourceValue
                : string.Empty;

            if (!filters.Sources.Contains(source, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static double CalculateScore(string query, float[]? queryEmbedding, IndexedChunk result)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(result.Content))
        {
            return queryEmbedding is { Length: > 0 } && result.Embedding is { Length: > 0 }
                ? CosineSimilarity(queryEmbedding, result.Embedding)
                : 0.1;
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matches = terms.Count(term => result.Content.Contains(term, StringComparison.OrdinalIgnoreCase));
        var lexicalScore = 0.4 + (matches / (double)Math.Max(terms.Length, 1)) * 0.6;

        if (queryEmbedding is not { Length: > 0 } || result.Embedding is not { Length: > 0 })
        {
            return Math.Round(lexicalScore, 2);
        }

        var vectorScore = CosineSimilarity(queryEmbedding, result.Embedding);
        return Math.Round((lexicalScore * 0.4) + (vectorScore * 0.6), 4);
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        if (length == 0)
        {
            return 0;
        }

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (var index = 0; index < length; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }

        if (leftNorm <= 0 || rightNorm <= 0)
        {
            return 0;
        }

        return (dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm)) + 1d) / 2d;
    }

    private sealed class IndexedChunk : SearchResultDto
    {
        public float[]? Embedding { get; init; }
    }
}
