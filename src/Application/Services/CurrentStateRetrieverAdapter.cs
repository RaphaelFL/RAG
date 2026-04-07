using System.Text;
using System.Text.Json;
using Chatbot.Application.Configuration;
using Chatbot.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Chatbot.Application.Services;

public sealed class CurrentStateRetrieverAdapter : IRetriever
{
    private readonly IRetrievalService _retrievalService;

    public CurrentStateRetrieverAdapter(IRetrievalService retrievalService)
    {
        _retrievalService = retrievalService;
    }

    public async Task<RetrievedContext> RetrieveAsync(RetrievalPlan request, CancellationToken ct)
    {
        var result = await _retrievalService.RetrieveAsync(new RetrievalQueryDto
        {
            Query = request.QueryText,
            TopK = request.TopK,
            DocumentIds = ReadGuidFilter(request.Filters, "documentIds"),
            Tags = ReadStringFilter(request.Filters, "tags"),
            Categories = ReadStringFilter(request.Filters, "categories"),
            ContentTypes = ReadStringFilter(request.Filters, "contentTypes"),
            Sources = ReadStringFilter(request.Filters, "sources"),
            SemanticRanking = request.UseHybridRetrieval || request.UseReranking
        }, ct);

        return new RetrievedContext
        {
            RetrievalStrategy = result.RetrievalStrategy,
            LatencyMs = result.LatencyMs,
            Chunks = result.Chunks.Select(chunk => new RetrievedChunk
            {
                ChunkId = chunk.ChunkId,
                DocumentId = chunk.DocumentId,
                Score = chunk.Score,
                Text = chunk.Content,
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["documentTitle"] = chunk.DocumentTitle,
                    ["section"] = chunk.Section ?? string.Empty,
                    ["page"] = chunk.PageNumber.ToString(),
                    ["endPage"] = chunk.EndPageNumber.ToString()
                }
            }).ToArray()
        };
    }

    private static List<Guid>? ReadGuidFilter(Dictionary<string, string[]> filters, string key)
    {
        if (!filters.TryGetValue(key, out var values) || values.Length == 0)
        {
            return null;
        }

        return values
            .Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .ToList();
    }

    private static List<string>? ReadStringFilter(Dictionary<string, string[]> filters, string key)
    {
        if (!filters.TryGetValue(key, out var values) || values.Length == 0)
        {
            return null;
        }

        return values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
    }
}
