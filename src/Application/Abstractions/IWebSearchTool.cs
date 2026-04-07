namespace Chatbot.Application.Abstractions;

public interface IWebSearchTool
{
    Task<WebSearchResult> SearchAsync(WebSearchRequest request, CancellationToken ct);
}