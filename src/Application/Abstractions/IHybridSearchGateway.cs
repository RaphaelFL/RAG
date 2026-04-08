namespace Chatbot.Application.Abstractions;

public interface IHybridSearchGateway
{
    Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct);
}