namespace Chatbot.Application.Services;

public sealed class SearchQueryService : ISearchQueryService
{
    private readonly IRetrievalService _retrievalService;

    public SearchQueryService(IRetrievalService retrievalService)
    {
        _retrievalService = retrievalService;
    }

    public async Task<SearchQueryResponseDto> QueryAsync(SearchQueryRequestDto query, CancellationToken ct)
    {
        var retrievalResult = await _retrievalService.RetrieveAsync(new RetrievalQueryDto
        {
            Query = query.Query,
            TopK = query.Top,
            DocumentIds = query.Filters?.DocumentIds,
            Tags = query.Filters?.Tags,
            Categories = query.Filters?.Categories,
            ContentTypes = query.Filters?.ContentTypes,
            Sources = query.Filters?.Sources,
            SemanticRanking = query.SemanticRanking
        }, ct);

        return new SearchQueryResponseDto
        {
            Items = retrievalResult.Chunks.Select(chunk => new SearchQueryItemDto
            {
                DocumentId = chunk.DocumentId,
                ChunkId = chunk.ChunkId,
                Title = chunk.DocumentTitle,
                Snippet = chunk.Content[..Math.Min(200, chunk.Content.Length)],
                Score = chunk.Score
            }).ToList(),
            Count = retrievalResult.Chunks.Count
        };
    }
}