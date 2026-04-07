using Chatbot.Application.Abstractions;

namespace Backend.Unit.GovernedAgentRuntimeTestsSupport;

internal sealed class CapturingWebSearchTool : IWebSearchTool
{
    public WebSearchRequest? LastRequest { get; private set; }

    public Task<WebSearchResult> SearchAsync(WebSearchRequest request, CancellationToken ct)
    {
        LastRequest = request;
        return Task.FromResult(new WebSearchResult
        {
            Hits = new[]
            {
                new WebSearchHit
                {
                    Title = "Resultado",
                    Url = "https://example.com",
                    Snippet = "Snippet",
                    Score = 0.8
                }
            }
        });
    }
}