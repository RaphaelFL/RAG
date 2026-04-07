namespace Chatbot.Application.Abstractions;

public interface ISearchQueryService
{
    Task<SearchQueryResponseDto> QueryAsync(SearchQueryRequestDto query, CancellationToken ct);
}