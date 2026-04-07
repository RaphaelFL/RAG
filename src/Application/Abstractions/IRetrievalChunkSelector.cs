namespace Chatbot.Application.Abstractions;

public interface IRetrievalChunkSelector
{
    IReadOnlyList<RetrievedChunkDto> Select(string query, IReadOnlyCollection<SearchResultDto> authorizedResults, FileSearchFilterDto filters, int requestedTopK);
}