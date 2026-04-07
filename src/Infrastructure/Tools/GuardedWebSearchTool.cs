using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Tools;

public sealed class GuardedWebSearchTool : IWebSearchTool
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex ResultAnchorRegex = new("""<a[^>]*class=["'][^"']*result__a[^"']*["'][^>]*href=["'](?<url>[^"']+)["'][^>]*>(?<title>.*?)</a>""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex GenericAnchorRegex = new("""<a[^>]*href=["'](?<url>https?://[^"']+)["'][^>]*>(?<title>.*?)</a>""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex SnippetRegex = new("""<(?:a|div|span)[^>]*class=["'][^"']*result__snippet[^"']*["'][^>]*>(?<snippet>.*?)</(?:a|div|span)>""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex ScriptStyleRegex = new("""<(script|style)[^>]*>.*?</\1>""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApplicationCache _cache;
    private readonly WebSearchOptions _options;
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
        var cacheKey = BuildCacheKey(request.TenantId, request.Query, topK);
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
            var candidates = await ParseCandidatesAsync(html, topK, ct);
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

    private async Task<IReadOnlyCollection<WebSearchHit>> ParseCandidatesAsync(string html, int topK, CancellationToken ct)
    {
        var hits = new List<WebSearchHit>();
        var matches = ResultAnchorRegex.Matches(html);
        if (matches.Count == 0)
        {
            matches = GenericAnchorRegex.Matches(html);
        }

        foreach (Match match in matches)
        {
            if (hits.Count >= topK)
            {
                break;
            }

            var rawUrl = WebUtility.HtmlDecode(match.Groups["url"].Value);
            var resolvedUrl = UnwrapSearchEngineUrl(rawUrl);
            if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri))
            {
                continue;
            }

            if (!await WebSearchSecurityPolicy.IsUriAllowedAsync(uri, _options, ct))
            {
                continue;
            }

            if (hits.Any(existing => string.Equals(existing.Url, uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var title = CleanHtml(match.Groups["title"].Value);
            var snippet = ExtractSnippetNear(html, match.Index, match.Length);
            if (string.IsNullOrWhiteSpace(snippet))
            {
                snippet = await TryFetchSnippetAsync(uri, ct);
            }

            hits.Add(new WebSearchHit
            {
                Title = string.IsNullOrWhiteSpace(title) ? uri.Host : title,
                Url = uri.AbsoluteUri,
                Snippet = snippet,
                Score = Math.Round(Math.Max(0.1d, 1d - (hits.Count * 0.08d)), 4)
            });
        }

        return hits;
    }

    private async Task<string> TryFetchSnippetAsync(Uri uri, CancellationToken ct)
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
            var text = CleanHtml(html);
            return text.Length > 320 ? text[..320] : text;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtractSnippetNear(string html, int index, int length)
    {
        var sliceStart = Math.Max(0, index + length);
        var sliceLength = Math.Min(2000, Math.Max(0, html.Length - sliceStart));
        if (sliceLength == 0)
        {
            return string.Empty;
        }

        var window = html.Substring(sliceStart, sliceLength);
        var snippetMatch = SnippetRegex.Match(window);
        return snippetMatch.Success ? CleanHtml(snippetMatch.Groups["snippet"].Value) : string.Empty;
    }

    private static string UnwrapSearchEngineUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        var uddg = TryGetQueryParameter(uri, "uddg");
        if (!string.IsNullOrWhiteSpace(uddg))
        {
            return Uri.UnescapeDataString(uddg);
        }

        var q = TryGetQueryParameter(uri, "q");
        if (uri.Host.Contains("google", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(q)
            && Uri.TryCreate(q, UriKind.Absolute, out var googleTarget))
        {
            return googleTarget.AbsoluteUri;
        }

        return url;
    }

    private static string? TryGetQueryParameter(Uri uri, string key)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var candidateKey = Uri.UnescapeDataString(pair[..separatorIndex]);
            if (!string.Equals(candidateKey, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
        }

        return null;
    }

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var sanitized = ScriptStyleRegex.Replace(html, " ");
        sanitized = HtmlTagRegex.Replace(sanitized, " ");
        sanitized = WebUtility.HtmlDecode(sanitized);
        sanitized = WhitespaceRegex.Replace(sanitized, " ");
        return sanitized.Trim();
    }

    private static string BuildCacheKey(Guid tenantId, string query, int topK)
    {
        var payload = Encoding.UTF8.GetBytes($"{tenantId:N}:{topK}:{query.Trim()}" );
        return $"web-search:{Convert.ToHexString(SHA256.HashData(payload))}";
    }

    private static WebSearchResult Empty()
    {
        return new WebSearchResult
        {
            Hits = Array.Empty<WebSearchHit>()
        };
    }
}

internal static class WebSearchSecurityPolicy
{
    public static async Task<bool> IsUriAllowedAsync(Uri uri, WebSearchOptions options, CancellationToken ct)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = uri.Host;
        if (IsLocalOrPrivateHostLiteral(host))
        {
            return false;
        }

        if (MatchesHost(host, options.DeniedHosts))
        {
            return false;
        }

        if (options.AllowedHosts.Length > 0)
        {
            return MatchesHost(host, options.AllowedHosts);
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, ct);
            return addresses.Length > 0 && addresses.All(address => !IsPrivateAddress(address));
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesHost(string host, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (string.Equals(host, candidate, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith($".{candidate}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLocalOrPrivateHostLiteral(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IsPrivateAddress(address);
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                10 => true,
                127 => true,
                169 when bytes[1] == 254 => true,
                172 when bytes[1] is >= 16 and <= 31 => true,
                192 when bytes[1] == 168 => true,
                _ => false
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast;
        }

        return false;
    }
}