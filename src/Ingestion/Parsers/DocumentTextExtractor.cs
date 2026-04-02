using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Chatbot.Ingestion.Parsers;

public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    private static readonly Regex PageMarkerRegex = new(@"^\s*(?:page|pagina|pg\.?)(?:\s+|\s*#\s*)(\d+)(?:\s*(?:/|de|of)\s*\d+)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PageNumberOnlyRegex = new(@"^\s*\d+\s*$", RegexOptions.Compiled);

    private readonly IEnumerable<IDocumentParser> _parsers;
    private readonly IOcrProvider _ocrProvider;
    private readonly OcrOptions _ocrOptions;

    public DocumentTextExtractor(IEnumerable<IDocumentParser> parsers, IOcrProvider ocrProvider, IOptions<OcrOptions> ocrOptions)
    {
        _parsers = parsers;
        _ocrProvider = ocrProvider;
        _ocrOptions = ocrOptions.Value;
    }

    public async Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        string? parsedText = null;

        foreach (var parser in _parsers)
        {
            if (!parser.CanParse(command))
            {
                continue;
            }

            parsedText = await parser.ParseAsync(command, ct);
            if (ShouldUseDirectText(command, parsedText))
            {
                if (ShouldRecordOcrAvoided(command))
                {
                    ChatbotTelemetry.OcrAvoided.Add(1, new KeyValuePair<string, object?>("content.type", command.ContentType));
                }

                var directExtraction = BuildExtraction(parsedText!, "direct", null, null);

                return directExtraction;
            }
        }

        if (!ShouldAttemptOcr(command, parsedText))
        {
            return BuildExtraction(parsedText ?? string.Empty, string.IsNullOrWhiteSpace(parsedText) ? "unavailable" : "direct", null, null);
        }

        var ocrResult = await _ocrProvider.ExtractAsync(command.Content, command.FileName, ct);
        return BuildExtraction(ocrResult.ExtractedText, "ocr", ocrResult.Provider ?? _ocrProvider.ProviderName, ocrResult.Pages);
    }

    private static DocumentTextExtractionResultDto BuildExtraction(
        string text,
        string strategy,
        string? provider,
        IReadOnlyCollection<PageExtractionDto>? rawPages)
    {
        var pages = NormalizePages(text, rawPages);
        var repeatedEdgeLines = FindRepeatedEdgeLines(pages);
        var cleanedPages = pages
            .Select(page => new PageExtractionDto
            {
                PageNumber = page.PageNumber,
                Text = CleanPageText(page.Text, repeatedEdgeLines),
                Tables = page.Tables
            })
            .Where(page => !string.IsNullOrWhiteSpace(page.Text))
            .ToList();

        if (cleanedPages.Count == 0)
        {
            var fallbackText = NormalizeWhitespaceBlock(text);
            cleanedPages.Add(new PageExtractionDto
            {
                PageNumber = 1,
                Text = fallbackText
            });
        }

        return new DocumentTextExtractionResultDto
        {
            Text = string.Join("\n\n", cleanedPages.Select(page => page.Text).Where(pageText => !string.IsNullOrWhiteSpace(pageText))).Trim(),
            Strategy = strategy,
            Provider = provider,
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

    private bool ShouldUseDirectText(IngestDocumentCommand command, string? parsedText)
    {
        if (string.IsNullOrWhiteSpace(parsedText))
        {
            return false;
        }

        if (IsPdfDocument(command) && PdfTextExtraction.LooksLikeArtifactText(parsedText))
        {
            return false;
        }

        if (!_ocrOptions.EnableSelectiveOcr || IsTextFirstDocument(command))
        {
            return true;
        }

        if (parsedText.Length >= _ocrOptions.MinimumDirectTextCharacters)
        {
            return true;
        }

        if (command.ContentLength <= 0)
        {
            return false;
        }

        var coverageRatio = parsedText.Length / (double)Math.Max(1, command.ContentLength);
        return coverageRatio >= _ocrOptions.MinimumDirectTextCoverageRatio;
    }

    private bool ShouldAttemptOcr(IngestDocumentCommand command, string? parsedText)
    {
        if (!_ocrOptions.EnableSelectiveOcr)
        {
            return true;
        }

        if (IsImageDocument(command))
        {
            return true;
        }

        if (IsTextFirstDocument(command))
        {
            return false;
        }

        if (IsPdfDocument(command))
        {
            return !ShouldUseDirectText(command, parsedText);
        }

        return false;
    }

    private static bool IsTextFirstDocument(IngestDocumentCommand command)
    {
        var extension = Path.GetExtension(command.FileName);
        return extension is ".txt" or ".md" or ".html" or ".htm";
    }

    private static bool IsPdfDocument(IngestDocumentCommand command)
    {
        return string.Equals(Path.GetExtension(command.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImageDocument(IngestDocumentCommand command)
    {
        var extension = Path.GetExtension(command.FileName);
        return command.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || extension is ".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff" or ".bmp";
    }

    private static bool ShouldRecordOcrAvoided(IngestDocumentCommand command)
    {
        return IsPdfDocument(command) || IsImageDocument(command);
    }
}