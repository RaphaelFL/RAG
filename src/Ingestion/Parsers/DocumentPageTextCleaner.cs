using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Parsers;

internal sealed class DocumentPageTextCleaner
{
    private readonly DocumentPageMarkerDetector _pageMarkerDetector;

    public DocumentPageTextCleaner(DocumentPageMarkerDetector pageMarkerDetector)
    {
        _pageMarkerDetector = pageMarkerDetector;
    }

    public HashSet<string> FindRepeatedEdgeLines(IReadOnlyCollection<PageExtractionDto> pages)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages)
        {
            var nonEmptyLines = page.Text
                .Split('\n')
                .Select(NormalizeInlineWhitespace)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            foreach (var candidate in nonEmptyLines.Take(2).Concat(nonEmptyLines.TakeLast(2)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!_pageMarkerDetector.IsHeaderFooterCandidate(candidate))
                {
                    continue;
                }

                counts[candidate] = counts.TryGetValue(candidate, out var current) ? current + 1 : 1;
            }
        }

        return counts
            .Where(entry => entry.Value >= 2)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public string Clean(string text, HashSet<string> repeatedEdgeLines)
    {
        var cleanedLines = new List<string>();
        string? lastContentLine = null;
        var previousBlank = true;

        foreach (var rawLine in NormalizeLineEndings(text).Split('\n'))
        {
            var normalizedLine = NormalizeInlineWhitespace(rawLine);
            if (string.IsNullOrWhiteSpace(normalizedLine))
            {
                if (!previousBlank && cleanedLines.Count > 0)
                {
                    cleanedLines.Add(string.Empty);
                }

                previousBlank = true;
                continue;
            }

            if (repeatedEdgeLines.Contains(normalizedLine) || _pageMarkerDetector.IsNoiseLine(normalizedLine))
            {
                continue;
            }

            if (string.Equals(lastContentLine, normalizedLine, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            cleanedLines.Add(normalizedLine);
            lastContentLine = normalizedLine;
            previousBlank = false;
        }

        return string.Join("\n", cleanedLines).Trim();
    }

    public string NormalizeWhitespaceBlock(string text)
    {
        return string.Join("\n", NormalizeLineEndings(text)
            .Split('\n')
            .Select(NormalizeInlineWhitespace)
            .Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string NormalizeLineEndings(string text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static string NormalizeInlineWhitespace(string line)
    {
        return string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim();
    }
}