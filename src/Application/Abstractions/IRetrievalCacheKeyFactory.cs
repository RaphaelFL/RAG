namespace Chatbot.Application.Abstractions;

public interface IRetrievalCacheKeyFactory
{
    string Build(string query, int requestedTopK, int candidateCount, bool semanticRankingEnabled, FileSearchFilterDto filters, IRequestContextAccessor requestContextAccessor, IDocumentCatalog documentCatalog);
}