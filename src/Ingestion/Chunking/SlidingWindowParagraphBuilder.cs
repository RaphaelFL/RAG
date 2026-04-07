using Chatbot.Application.Abstractions;
using System.Text.RegularExpressions;

namespace Chatbot.Ingestion.Chunking;

internal sealed class SlidingWindowParagraphBuilder
{
    private static readonly Regex SectionPrefixRegex = new(@"^(?:#+\s*|\d+(?:\.\d+)*\.?\s+)", RegexOptions.Compiled);
    private static readonly Regex SentenceBoundaryRegex = new(@"(?<=[\.!\?])\s+", RegexOptions.Compiled);

    public string NormalizeSourceText(IngestDocumentCommand command, string text)
    {
        var source = string.IsNullOrWhiteSpace(text) ? command.FileName : text;
        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        return string.Join('\n', normalized.Split('\n').Select(line => line.TrimEnd()));
    }

    public List<SemanticParagraph> Build(IngestDocumentCommand command, DocumentTextExtractionResultDto extraction, int maxTokens)
    {
        var sourceText = NormalizeSourceText(command, extraction.Text);
        var pages = extraction.Pages.Count > 0
            ? extraction.Pages.Where(page => !string.IsNullOrWhiteSpace(page.Text)).OrderBy(page => page.PageNumber).ToList()
            : new List<PageExtractionDto>
            {
                new()
                {
                    PageNumber = 1,
                    Text = sourceText
                }
            };

        return ExpandLargeParagraphs(BuildSemanticParagraphs(command, pages), maxTokens);
    }

    private List<SemanticParagraph> BuildSemanticParagraphs(IngestDocumentCommand command, IReadOnlyCollection<PageExtractionDto> pages)
    {
        var paragraphs = new List<SemanticParagraph>();
        var currentSection = ResolveDocumentTitle(command);

        foreach (var page in pages)
        {
            var pageSection = string.IsNullOrWhiteSpace(page.SectionTitle) ? currentSection : page.SectionTitle!;
            foreach (var paragraph in SplitParagraphs(page.Text))
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    continue;
                }

                if (IsHeading(paragraph))
                {
                    currentSection = NormalizeHeading(paragraph);
                    paragraphs.Add(CreateParagraph(currentSection, page.PageNumber, page.PageNumber, currentSection, true, page));
                    continue;
                }

                paragraphs.Add(CreateParagraph(paragraph, page.PageNumber, page.PageNumber, pageSection, false, page));
            }
        }

        return paragraphs;
    }

    private List<SemanticParagraph> ExpandLargeParagraphs(IEnumerable<SemanticParagraph> paragraphs, int maxTokens)
    {
        var expanded = new List<SemanticParagraph>();

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.EstimatedTokens <= maxTokens)
            {
                expanded.Add(paragraph);
                continue;
            }

            var buffer = new List<string>();
            var bufferTokens = 0;

            foreach (var sentence in SentenceBoundaryRegex.Split(paragraph.Text).Where(sentence => !string.IsNullOrWhiteSpace(sentence)))
            {
                var normalizedSentence = NormalizeParagraph(sentence);
                var sentenceTokens = EstimateTokens(normalizedSentence);
                if (buffer.Count > 0 && bufferTokens + sentenceTokens > maxTokens)
                {
                    expanded.Add(new SemanticParagraph(string.Join(' ', buffer), paragraph.StartPage, paragraph.EndPage, paragraph.Section, paragraph.IsHeading, paragraph.WorksheetName, paragraph.SlideNumber, paragraph.TableId, paragraph.FormId, EstimateTokens(string.Join(' ', buffer))));
                    buffer.Clear();
                    bufferTokens = 0;
                }

                buffer.Add(normalizedSentence);
                bufferTokens += sentenceTokens;
            }

            if (buffer.Count > 0)
            {
                expanded.Add(new SemanticParagraph(string.Join(' ', buffer), paragraph.StartPage, paragraph.EndPage, paragraph.Section, paragraph.IsHeading, paragraph.WorksheetName, paragraph.SlideNumber, paragraph.TableId, paragraph.FormId, EstimateTokens(string.Join(' ', buffer))));
            }
        }

        return expanded;
    }

    private static IEnumerable<string> SplitParagraphs(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<string>();
        }

        return normalized
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(SplitBlock)
            .Where(block => !string.IsNullOrWhiteSpace(block));
    }

    private static IEnumerable<string> SplitBlock(string block)
    {
        var lines = block
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
        {
            yield break;
        }

        if (lines.Count > 1 && IsHeading(lines[0]))
        {
            yield return NormalizeHeading(lines[0]);
            yield return NormalizeParagraph(string.Join(' ', lines.Skip(1)));
            yield break;
        }

        yield return NormalizeParagraph(string.Join(' ', lines));
    }

    private static bool IsHeading(string paragraph)
    {
        var original = paragraph.Trim();
        var normalized = NormalizeHeading(paragraph);
        if (normalized.Length is < 3 or > 120)
        {
            return false;
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length > 12)
        {
            return false;
        }

        if (original.StartsWith('#') || SectionPrefixRegex.IsMatch(original))
        {
            return true;
        }

        if (normalized.EndsWith(':'))
        {
            return true;
        }

        return normalized.Any(char.IsLetter)
            && normalized.Equals(normalized.ToUpperInvariant(), StringComparison.Ordinal)
            && words.Length <= 10;
    }

    private static string NormalizeHeading(string paragraph)
    {
        var normalized = NormalizeParagraph(paragraph);
        normalized = SectionPrefixRegex.Replace(normalized, string.Empty);
        return normalized.Trim(' ', '-', ':');
    }

    private static string NormalizeParagraph(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n')
            .Select(line => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join('\n', lines).Trim();
    }

    private static int EstimateTokens(string text)
    {
        return Math.Max(1, (int)Math.Ceiling(text.Length / 4d));
    }

    private static string ResolveDocumentTitle(IngestDocumentCommand command)
    {
        return string.IsNullOrWhiteSpace(command.DocumentTitle) ? command.FileName : command.DocumentTitle;
    }

    private static SemanticParagraph CreateParagraph(string text, int startPage, int endPage, string section, bool isHeading, PageExtractionDto page)
    {
        return new SemanticParagraph(text, startPage, endPage, section, isHeading, page.WorksheetName, page.SlideNumber, page.TableId, page.FormId, EstimateTokens(text));
    }
}