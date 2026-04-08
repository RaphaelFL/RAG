using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Parsers;

internal sealed class DocumentPageNormalizer
{
    private readonly DocumentPageMarkerDetector _pageMarkerDetector;

    public DocumentPageNormalizer(DocumentPageMarkerDetector pageMarkerDetector)
    {
        _pageMarkerDetector = pageMarkerDetector;
    }

    public List<PageExtractionDto> Normalize(string text, IReadOnlyCollection<PageExtractionDto>? rawPages)
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

    private List<PageExtractionDto> SplitIntoPages(string text)
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
            if (_pageMarkerDetector.TryParseStandalonePageMarker(line, out var markerPageNumber) && buffer.Count > 0)
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

    private static string NormalizeLineEndings(string text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}