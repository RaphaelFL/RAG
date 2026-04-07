namespace Chatbot.Application.Services;

public sealed class RetrievalChunkSelector : IRetrievalChunkSelector
{
    private readonly IRagRuntimeSettings _ragRuntimeSettings;

    public RetrievalChunkSelector(IRagRuntimeSettings ragRuntimeSettings)
    {
        _ragRuntimeSettings = ragRuntimeSettings;
    }

    public IReadOnlyList<RetrievedChunkDto> Select(string query, IReadOnlyCollection<SearchResultDto> authorizedResults, FileSearchFilterDto filters, int requestedTopK)
    {
        var rerankedResults = authorizedResults
            .Select(result => new
            {
                Result = result,
                Score = ComputeRerankedScore(query, result, filters)
            })
            .Where(item => item.Score >= _ragRuntimeSettings.MinimumRerankScore)
            .OrderByDescending(item => item.Score)
            .Take(requestedTopK)
            .ToList();

        if (rerankedResults.Count == 0)
        {
            rerankedResults = authorizedResults
                .OrderByDescending(result => result.Score)
                .Take(requestedTopK)
                .Select(result => new { Result = result, Score = result.Score })
                .ToList();
        }

        return rerankedResults
            .Select(item => new RetrievedChunkDto
            {
                ChunkId = item.Result.ChunkId,
                DocumentId = item.Result.DocumentId,
                Content = item.Result.Content,
                Score = item.Score,
                DocumentTitle = item.Result.Metadata.ContainsKey("title") ? item.Result.Metadata["title"] : "Unknown",
                PageNumber = ParseMetadataInt(item.Result.Metadata, "startPage"),
                EndPageNumber = ParseMetadataInt(item.Result.Metadata, "endPage"),
                Section = item.Result.Metadata.TryGetValue("section", out var sectionValue) ? sectionValue : string.Empty
            })
            .ToList();
    }

    private double ComputeRerankedScore(string query, SearchResultDto result, FileSearchFilterDto filters)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (terms.Length == 0)
        {
            return result.Score;
        }

        var title = result.Metadata.TryGetValue("title", out var titleValue) ? titleValue : string.Empty;
        var tags = GetMetadataValues(result.Metadata, "tags");
        var categories = GetMetadataValues(result.Metadata, "categories");
        var category = result.Metadata.TryGetValue("category", out var categoryValue) ? categoryValue : string.Empty;
        var contentType = result.Metadata.TryGetValue("contentType", out var contentTypeValue) ? contentTypeValue : string.Empty;
        var source = result.Metadata.TryGetValue("source", out var sourceValue) ? sourceValue : string.Empty;
        var exactMatches = terms.Count(term => result.Content.Contains(term, StringComparison.OrdinalIgnoreCase));
        var titleMatches = terms.Any(term => title.Contains(term, StringComparison.OrdinalIgnoreCase));
        var filterMatches = 0;

        if (filters.Tags is { Count: > 0 } && filters.Tags.Any(tag => tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
        {
            filterMatches++;
        }

        if (filters.Categories is { Count: > 0 }
            && (filters.Categories.Any(filter => categories.Contains(filter, StringComparer.OrdinalIgnoreCase))
                || filters.Categories.Any(filter => string.Equals(filter, category, StringComparison.OrdinalIgnoreCase))))
        {
            filterMatches++;
        }

        if (filters.ContentTypes is { Count: > 0 }
            && filters.ContentTypes.Any(filter => string.Equals(filter, contentType, StringComparison.OrdinalIgnoreCase)))
        {
            filterMatches++;
        }

        if (filters.Sources is { Count: > 0 }
            && filters.Sources.Any(filter => string.Equals(filter, source, StringComparison.OrdinalIgnoreCase)))
        {
            filterMatches++;
        }

        var score = result.Score + (exactMatches / (double)terms.Length) * _ragRuntimeSettings.ExactMatchBoost;
        if (titleMatches)
        {
            score += _ragRuntimeSettings.TitleMatchBoost;
        }

        if (filterMatches > 0)
        {
            score += filterMatches * _ragRuntimeSettings.FilterMatchBoost;
        }

        return Math.Round(score, 4);
    }

    private static string[] GetMetadataValues(Dictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var rawValue)
            ? rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();
    }

    private static int ParseMetadataInt(Dictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, out var parsed)
            ? parsed
            : 1;
    }
}