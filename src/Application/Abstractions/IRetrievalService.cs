namespace Chatbot.Application.Abstractions;

public interface IRetrievalService
{
    Task<RetrievalResultDto> RetrieveAsync(RetrievalQueryDto query, CancellationToken ct);
}