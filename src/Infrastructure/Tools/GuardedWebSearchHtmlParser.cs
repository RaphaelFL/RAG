using System.Net;
using System.Text.RegularExpressions;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure.Tools;

internal sealed class GuardedWebSearchHtmlParser
{
    private static readonly Regex ResultAnchorRegex = new("""<a[^>]*class=["'][^"']*result__a[^"']*["'][^>]*href=["'](?<url>[^"']+)["'][^>]*>(?<title>.*?)</a>""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex GenericAnchorRegex = new("""<a[^>]*href=["'](?<url>https?://[^"']+)["'][^>]*>(?<title>.*?)</a>""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex SnippetRegex = new("""<(?:a|div|span)[^>]*class=["'][^"']*result__snippet[^"']*["'][^>]*>(?<snippet>.*?)</(?:a|div|span)>""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex ScriptStyleRegex = new("""<(script|style)[^>]*>.*?</\1>""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public async Task<IReadOnlyCollection<WebSearchHit>> ParseCandidatesAsync(
        string html,
        int topK,
        WebSearchOptions options,
        GuardedWebSearchSnippetFetcher snippetFetcher,
        CancellationToken ct)
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

            var uri = ResolveUri(match.Groups["url"].Value);
            if (uri is null)
            {
                continue;
            }

            if (!await WebSearchSecurityPolicy.IsUriAllowedAsync(uri, options, ct))
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
                snippet = await snippetFetcher.TryFetchAsync(uri, ct);
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

    public string CleanHtml(string html)
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

    private static Uri? ResolveUri(string rawUrl)
    {
        var resolvedUrl = UnwrapSearchEngineUrl(WebUtility.HtmlDecode(rawUrl));
        return Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri) ? uri : null;
    }

    private string ExtractSnippetNear(string html, int index, int length)
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
}