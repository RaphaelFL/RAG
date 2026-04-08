using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Parsers;

public sealed class DocumentExtractionResultBuilder : IDocumentExtractionResultBuilder
{
    private readonly DocumentPageNormalizer _pageNormalizer = new(new DocumentPageMarkerDetector());
    private readonly DocumentPageTextCleaner _pageTextCleaner = new(new DocumentPageMarkerDetector());

    public DocumentTextExtractionResultDto Build(string text, string strategy, string? provider, IReadOnlyCollection<PageExtractionDto>? rawPages, string? structuredJson)
    {
        var pages = _pageNormalizer.Normalize(text, rawPages);
        var repeatedEdgeLines = _pageTextCleaner.FindRepeatedEdgeLines(pages);
        var cleanedPages = pages
            .Select(page => new PageExtractionDto
            {
                PageNumber = page.PageNumber,
                Text = _pageTextCleaner.Clean(page.Text, repeatedEdgeLines),
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
                Text = _pageTextCleaner.NormalizeWhitespaceBlock(text)
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
}