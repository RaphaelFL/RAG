namespace Chatbot.Application.Abstractions;

public interface IRetriever
{
    Task<RetrievedContext> RetrieveAsync(RetrievalPlan request, CancellationToken ct);
}