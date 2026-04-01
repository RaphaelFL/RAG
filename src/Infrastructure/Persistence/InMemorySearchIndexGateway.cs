using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

public sealed class InMemorySearchIndexGateway : ISearchIndexGateway
{
    private static readonly List<SearchResultDto> Index = new();
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
                Index.Add(new SearchResultDto
                {
                    ChunkId = chunk.ChunkId,
                    DocumentId = chunk.DocumentId,
                    Content = chunk.Content,
                    Score = 0.95,
                    Metadata = new Dictionary<string, string>(chunk.Metadata)
                });
            }
        }

        return Task.CompletedTask;
    }

    public Task<List<SearchResultDto>> HybridSearchAsync(string query, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        List<SearchResultDto> candidateResults;
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
                Score = CalculateScore(query, result.Content)
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

    private List<SearchResultDto> BuildFallbackResults(string query, FileSearchFilterDto? filters)
    {
        var documents = _documentCatalog.Query(filters);
        if (documents.Count == 0)
        {
            return new List<SearchResultDto>();
        }

        return documents
            .SelectMany(document => document.Chunks)
            .Select(chunk => new SearchResultDto
            {
                ChunkId = chunk.ChunkId,
                DocumentId = chunk.DocumentId,
                Content = chunk.Content,
                Score = 0.9,
                Metadata = chunk.Metadata
            })
            .ToList();
    }

    private static bool MatchesFilters(SearchResultDto result, FileSearchFilterDto? filters)
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

        return true;
    }

    private static double CalculateScore(string query, string content)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(content))
        {
            return 0.1;
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matches = terms.Count(term => content.Contains(term, StringComparison.OrdinalIgnoreCase));
        return Math.Round(0.4 + (matches / (double)Math.Max(terms.Length, 1)) * 0.6, 2);
    }
}
