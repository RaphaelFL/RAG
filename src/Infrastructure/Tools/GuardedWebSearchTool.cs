using System.Net;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Tools;

public sealed class GuardedWebSearchTool : IWebSearchTool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApplicationCache _cache;
    private readonly WebSearchOptions _options;
    private readonly GuardedWebSearchCacheKeyBuilder _cacheKeyBuilder = new();
    private readonly GuardedWebSearchHtmlParser _htmlParser = new();
    private readonly GuardedWebSearchSnippetFetcher _snippetFetcher;
    private readonly ILogger<GuardedWebSearchTool> _logger;

    public GuardedWebSearchTool(
        IHttpClientFactory httpClientFactory,
        IApplicationCache cache,
        IOptions<WebSearchOptions> options,
        ILogger<GuardedWebSearchTool> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options.Value;
        _snippetFetcher = new GuardedWebSearchSnippetFetcher(httpClientFactory, _htmlParser);
        _logger = logger;
    }

    public async Task<WebSearchResult> SearchAsync(WebSearchRequest request, CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(request.Query))
        {
            return new WebSearchResult
            {
                Hits = Array.Empty<WebSearchHit>()
            };
        }

        var topK = request.TopK > 0 ? request.TopK : Math.Max(1, _options.DefaultTopK);
        var cacheKey = _cacheKeyBuilder.Build(request.TenantId, request.Query, topK);
        var cached = await _cache.GetAsync<WebSearchResult>(cacheKey, ct);
        if (cached is not null)
        {
            return cached;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("WebSearch");
            var searchUri = new Uri($"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(request.Query)}");
            using var searchResponse = await client.GetAsync(searchUri, ct);
            if (!searchResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Web search retornou status {StatusCode} para query {Query}.", (int)searchResponse.StatusCode, request.Query);
                return Empty();
            }

            var html = await searchResponse.Content.ReadAsStringAsync(ct);
            var candidates = await _htmlParser.ParseCandidatesAsync(html, topK, _options, _snippetFetcher, ct);
            var result = new WebSearchResult
            {
                Hits = candidates
            };

            await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10), ct);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha na execucao de web search para query {Query}.", request.Query);
            return Empty();
        }
    }

    private static WebSearchResult Empty()
    {
        return new WebSearchResult
        {
            Hits = Array.Empty<WebSearchHit>()
        };
    }
}