namespace Chatbot.Application.Abstractions;

public sealed class WebSearchResult
{
    public IReadOnlyCollection<WebSearchHit> Hits { get; set; } = Array.Empty<WebSearchHit>();
}
