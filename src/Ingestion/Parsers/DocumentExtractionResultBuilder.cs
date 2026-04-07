using Chatbot.Application.Abstractions;
using System.Text.RegularExpressions;

namespace Chatbot.Ingestion.Parsers;

public sealed class DocumentExtractionResultBuilder : IDocumentExtractionResultBuilder
{
    private static readonly Regex PageMarkerRegex = new(@"^\s*(?:page|pagina|pg\.?)(?:\s+|\s*#\s*)(\d+)(?:\s*(?:/|de|of)\s*\d+)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PageNumberOnlyRegex = new(@"^\s*\d+\s*$", RegexOptions.Compiled);

    public DocumentTextExtractionResultDto Build(string text, string strategy, string? provider, IReadOnlyCollection<PageExtractionDto>? rawPages, string? structuredJson)
    {
        var pages = NormalizePages(text, rawPages);
        var repeatedEdgeLines = FindRepeatedEdgeLines(pages);
        var cleanedPages = pages
            .Select(page => new PageExtractionDto
            {
                PageNumber = page.PageNumber,
                Text = CleanPageText(page.Text, repeatedEdgeLines),
                WorksheetName = page.WorksheetName,
                SlideNumber = page.SlideNumber,
                SectionTitle = page.SectionTitle,
                TableId = page.TableId,
                FormId = page.FormId,
                Metadata = new Dictionary<string, string>(page.Metadata, StringComparer.OrdinalIgnoreCase),
                Tables = page.Tables
            })
            .Where(page => !string.IsNullOrWhiteSpace(page.Text))
            .ToList();

        if (cleanedPages.Count == 0)
        {
            cleanedPages.Add(new PageExtractionDto
            {
                PageNumber = 1,
                Text = NormalizeWhitespaceBlock(text)
            });
        }

        return new DocumentTextExtractionResultDto
        {
            Text = string.Join("\n\n", cleanedPages.Select(page => page.Text).Where(pageText => !string.IsNullOrWhiteSpace(pageText))).Trim(),
            Strategy = strategy,
            Provider = provider,
            StructuredJson = structuredJson,
            Pages = cleanedPages
        };
    }

    private static List<PageExtractionDto> NormalizePages(string text, IReadOnlyCollection<PageExtractionDto>? rawPages)
    {
        if (rawPages is { Count: > 0 })
        {
            var pages = rawPages
                .Select((page, index) => new PageExtractionDto
                {
                    PageNumber = page.PageNumber > 0 ? page.PageNumber : index + 1,
                    Text = NormalizeLineEndings(page.Text),
                    WorksheetName = page.WorksheetName,
                    SlideNumber = page.SlideNumber,
                    SectionTitle = page.SectionTitle,
                    TableId = page.TableId,
                    FormId = page.FormId,
                    Metadata = new Dictionary<string, string>(page.Metadata, StringComparer.OrdinalIgnoreCase),
                    Tables = page.Tables
                })
                .ToList();

            return pages.Count == 0 ? SplitIntoPages(text) : pages;
        }

        return SplitIntoPages(text);
    }

    private static List<PageExtractionDto> SplitIntoPages(string text)
    {
        var normalized = NormalizeLineEndings(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new List<PageExtractionDto>();
        }

        var formFeedPages = normalized
            .Split('\f', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (formFeedPages.Count > 1)
        {
            return formFeedPages
                .Select((pageText, index) => new PageExtractionDto
                {
                    PageNumber = index + 1,
                    Text = pageText
                })
                .ToList();
        }

        var pages = new List<PageExtractionDto>();
        var buffer = new List<string>();
        var currentPage = 1;

        foreach (var rawLine in normalized.Split('\n'))
        {
            var line = rawLine.Trim();
            if (TryParseStandalonePageMarker(line, out var markerPageNumber) && buffer.Count > 0)
            {
                pages.Add(new PageExtractionDto
                {
                    PageNumber = currentPage,
                    Text = string.Join('\n', buffer)
                });
                buffer.Clear();
                currentPage = markerPageNumber > 0 ? markerPageNumber : currentPage + 1;
                continue;
            }

            buffer.Add(rawLine);
        }

        if (buffer.Count > 0)
        {
            pages.Add(new PageExtractionDto
            {
                PageNumber = currentPage,
                Text = string.Join('\n', buffer)
            });
        }

        return pages.Count == 0
            ? new List<PageExtractionDto>
            {
                new()
                {
                    PageNumber = 1,
                    Text = normalized
                }
            }
            : pages;
    }

    private static HashSet<string> FindRepeatedEdgeLines(IReadOnlyCollection<PageExtractionDto> pages)
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
                if (!IsHeaderFooterCandidate(candidate))
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

    private static string CleanPageText(string text, HashSet<string> repeatedEdgeLines)
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

            if (repeatedEdgeLines.Contains(normalizedLine) || IsNoiseLine(normalizedLine))
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

    private static bool IsHeaderFooterCandidate(string line)
    {
        return line.Length is >= 3 and <= 120
            && line.Any(char.IsLetter)
            && !IsNoiseLine(line);
    }

    private static bool IsNoiseLine(string line)
    {
        return PageNumberOnlyRegex.IsMatch(line)
            || TryParseStandalonePageMarker(line, out _)
            || line.Equals("confidencial", StringComparison.OrdinalIgnoreCase)
            || line.Equals("confidential", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseStandalonePageMarker(string line, out int pageNumber)
    {
        var match = PageMarkerRegex.Match(line);
        if (match.Success && int.TryParse(match.Groups[1].Value, out pageNumber))
        {
            return true;
        }

        pageNumber = 0;
        return false;
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

    private static string NormalizeWhitespaceBlock(string text)
    {
        return string.Join("\n", NormalizeLineEndings(text)
            .Split('\n')
            .Select(NormalizeInlineWhitespace)
            .Where(line => !string.IsNullOrWhiteSpace(line)));
    }
}