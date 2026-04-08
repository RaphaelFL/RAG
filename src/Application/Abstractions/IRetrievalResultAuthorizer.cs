namespace Chatbot.Application.Abstractions;

public interface IRetrievalResultAuthorizer
{
    IReadOnlyList<SearchResultDto> Authorize(IReadOnlyCollection<SearchResultDto> results);
}