namespace Chatbot.Infrastructure.Tools;

internal sealed class GuardedWebSearchSnippetFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GuardedWebSearchHtmlParser _htmlParser;

    public GuardedWebSearchSnippetFetcher(IHttpClientFactory httpClientFactory, GuardedWebSearchHtmlParser htmlParser)
    {
        _httpClientFactory = httpClientFactory;
        _htmlParser = htmlParser;
    }

    public async Task<string> TryFetchAsync(Uri uri, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("WebSearch");
            using var response = await client.GetAsync(uri, ct);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            var text = _htmlParser.CleanHtml(html);
            return text.Length > 320 ? text[..320] : text;
        }
        catch
        {
            return string.Empty;
        }
    }
}