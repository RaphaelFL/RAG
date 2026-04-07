namespace Chatbot.Application.Abstractions;

public interface IReranker
{
    Task<IReadOnlyCollection<RetrievedChunk>> RerankAsync(RerankRequest request, CancellationToken ct);
}