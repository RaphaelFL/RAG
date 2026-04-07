using Chatbot.Application.Abstractions;
using System.Text.RegularExpressions;

namespace Chatbot.Ingestion.Chunking;

public sealed class SlidingWindowChunkingStrategy : IChunkingStrategy
{
    private static readonly Regex SectionPrefixRegex = new(@"^(?:#+\s*|\d+(?:\.\d+)*\.?\s+)", RegexOptions.Compiled);
    private static readonly Regex SentenceBoundaryRegex = new(@"(?<=[\.!\?])\s+", RegexOptions.Compiled);

    private readonly IRagRuntimeSettings _runtimeSettings;

    public SlidingWindowChunkingStrategy(IRagRuntimeSettings runtimeSettings)
    {
        _runtimeSettings = runtimeSettings;
    }

    public List<DocumentChunkIndexDto> Chunk(IngestDocumentCommand command, DocumentTextExtractionResultDto extraction)
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
        var paragraphs = ExpandLargeParagraphs(BuildSemanticParagraphs(command, pages), ResolveMaxTokens(sourceText));
        var chunks = BuildChunks(command, sourceText, paragraphs);

        if (chunks.Count == 0)
        {
            chunks.Add(new DocumentChunkIndexDto
            {
                ChunkId = $"{command.DocumentId:N}-chunk-0001",
                DocumentId = command.DocumentId,
                Content = sourceText,
                PageNumber = 1,
                Section = ResolveDocumentTitle(command),
                Metadata = BuildMetadata(command, ResolveDocumentTitle(command), 1, 1, EstimateTokens(sourceText))
            });
        }

        return chunks;
    }

    private (int ChunkSize, int Overlap) ResolveWindow(string text)
    {
        return IsDenseContent(text)
            ? (Math.Max(_runtimeSettings.MinimumChunkCharacters, _runtimeSettings.DenseChunkSize), Math.Max(0, _runtimeSettings.DenseOverlap))
            : (Math.Max(_runtimeSettings.MinimumChunkCharacters, _runtimeSettings.NarrativeChunkSize), Math.Max(0, _runtimeSettings.NarrativeOverlap));
    }

    private int ResolveMaxTokens(string text)
    {
        var (chunkSize, _) = ResolveWindow(text);
        return Math.Max(32, EstimateTokens(chunkSize));
    }

    private int ResolveOverlapTokens(string text)
    {
        var (_, overlap) = ResolveWindow(text);
        return Math.Max(8, EstimateTokens(Math.Max(0, overlap)));
    }

    private static string NormalizeSourceText(IngestDocumentCommand command, string text)
    {
        var source = string.IsNullOrWhiteSpace(text) ? command.FileName : text;
        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        return string.Join('\n', normalized.Split('\n').Select(line => line.TrimEnd()));
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
                    paragraphs.Add(new SemanticParagraph(currentSection, page.PageNumber, page.PageNumber, currentSection, true, page.WorksheetName, page.SlideNumber, page.TableId, page.FormId));
                    continue;
                }

                paragraphs.Add(new SemanticParagraph(paragraph, page.PageNumber, page.PageNumber, pageSection, false, page.WorksheetName, page.SlideNumber, page.TableId, page.FormId));
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
                    expanded.Add(new SemanticParagraph(string.Join(' ', buffer), paragraph.StartPage, paragraph.EndPage, paragraph.Section, paragraph.IsHeading, paragraph.WorksheetName, paragraph.SlideNumber, paragraph.TableId, paragraph.FormId));
                    buffer.Clear();
                    bufferTokens = 0;
                }

                buffer.Add(normalizedSentence);
                bufferTokens += sentenceTokens;
            }

            if (buffer.Count > 0)
            {
                expanded.Add(new SemanticParagraph(string.Join(' ', buffer), paragraph.StartPage, paragraph.EndPage, paragraph.Section, paragraph.IsHeading, paragraph.WorksheetName, paragraph.SlideNumber, paragraph.TableId, paragraph.FormId));
            }
        }

        return expanded;
    }

    private List<DocumentChunkIndexDto> BuildChunks(IngestDocumentCommand command, string sourceText, IReadOnlyList<SemanticParagraph> paragraphs)
    {
        var chunks = new List<DocumentChunkIndexDto>();
        var maxTokens = ResolveMaxTokens(sourceText);
        var overlapTokens = ResolveOverlapTokens(sourceText);
        var currentParagraphs = new List<SemanticParagraph>();
        var currentTokens = 0;

        foreach (var paragraph in paragraphs)
        {
            var paragraphTokens = paragraph.EstimatedTokens;
            if (currentParagraphs.Count > 0 && currentTokens + paragraphTokens > maxTokens)
            {
                chunks.Add(CreateChunk(command, currentParagraphs, chunks.Count + 1));
                currentParagraphs = BuildOverlapSeed(currentParagraphs, overlapTokens);
                currentTokens = currentParagraphs.Sum(item => item.EstimatedTokens);
            }

            currentParagraphs.Add(paragraph);
            currentTokens += paragraphTokens;
        }

        if (currentParagraphs.Count > 0)
        {
            chunks.Add(CreateChunk(command, currentParagraphs, chunks.Count + 1));
        }

        return chunks;
    }

    private static List<SemanticParagraph> BuildOverlapSeed(IReadOnlyList<SemanticParagraph> paragraphs, int overlapTokens)
    {
        if (paragraphs.Count == 0 || overlapTokens <= 0)
        {
            return new List<SemanticParagraph>();
        }

        var overlap = new List<SemanticParagraph>();
        var totalTokens = 0;

        for (var index = paragraphs.Count - 1; index >= 0; index--)
        {
            var paragraph = paragraphs[index];
            overlap.Insert(0, paragraph);
            totalTokens += paragraph.EstimatedTokens;
            if (totalTokens >= overlapTokens)
            {
                break;
            }
        }

        return overlap;
    }

    private DocumentChunkIndexDto CreateChunk(IngestDocumentCommand command, IReadOnlyList<SemanticParagraph> paragraphs, int index)
    {
        var startPage = paragraphs.Min(paragraph => paragraph.StartPage);
        var endPage = paragraphs.Max(paragraph => paragraph.EndPage);
        var section = paragraphs.Select(paragraph => paragraph.Section).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? ResolveDocumentTitle(command);
        var worksheetName = paragraphs.Select(paragraph => paragraph.WorksheetName).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var slideNumber = paragraphs.Select(paragraph => paragraph.SlideNumber).FirstOrDefault(value => value.HasValue);
        var tableId = paragraphs.Select(paragraph => paragraph.TableId).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var formId = paragraphs.Select(paragraph => paragraph.FormId).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var content = string.Join("\n\n", paragraphs.Select(paragraph => paragraph.Text)).Trim();
        var metadata = BuildMetadata(command, section, startPage, endPage, EstimateTokens(content));
        metadata["chunkIndex"] = index.ToString();
        metadata["chunkingStrategy"] = nameof(SlidingWindowChunkingStrategy);

        if (!string.IsNullOrWhiteSpace(worksheetName))
        {
            metadata["worksheetName"] = worksheetName;
        }

        if (slideNumber.HasValue)
        {
            metadata["slideNumber"] = slideNumber.Value.ToString();
        }

        if (!string.IsNullOrWhiteSpace(tableId))
        {
            metadata["tableId"] = tableId;
        }

        if (!string.IsNullOrWhiteSpace(formId))
        {
            metadata["formId"] = formId;
        }

        return new DocumentChunkIndexDto
        {
            ChunkId = $"{command.DocumentId:N}-chunk-{index:D4}",
            DocumentId = command.DocumentId,
            Content = content,
            PageNumber = startPage,
            Section = section,
            Metadata = metadata
        };
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
            .SelectMany(block => SplitBlock(block))
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

    private static int EstimateTokens(int characterCount)
    {
        return Math.Max(1, (int)Math.Ceiling(characterCount / 4d));
    }

    private static string ResolveDocumentTitle(IngestDocumentCommand command)
    {
        return string.IsNullOrWhiteSpace(command.DocumentTitle) ? command.FileName : command.DocumentTitle;
    }

    private static bool IsDenseContent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lineBreaks = text.Count(character => character == '\n');
        var digits = text.Count(char.IsDigit);
        var bulletMarkers = text.Count(character => character is '-' or '*' or '•');
        var lineBreakRatio = lineBreaks / (double)Math.Max(1, text.Length);
        var digitRatio = digits / (double)Math.Max(1, text.Length);

        return lineBreakRatio > 0.02 || digitRatio > 0.08 || bulletMarkers >= 8;
    }

    private static Dictionary<string, string> BuildMetadata(IngestDocumentCommand command, string section, int startPage, int endPage, int estimatedTokens)
    {
        return new Dictionary<string, string>
        {
            ["title"] = ResolveDocumentTitle(command),
            ["sourceName"] = command.FileName,
            ["originalFileName"] = command.FileName,
            ["contentType"] = command.ContentType,
            ["tenantId"] = command.TenantId.ToString(),
            ["category"] = command.Category ?? string.Empty,
            ["source"] = command.Source ?? string.Empty,
            ["sourceType"] = command.Source ?? string.Empty,
            ["accessPolicy"] = command.AccessPolicy ?? string.Empty,
            ["section"] = section,
            ["startPage"] = startPage.ToString(),
            ["endPage"] = endPage.ToString(),
            ["estimatedTokens"] = estimatedTokens.ToString(),
            ["tags"] = string.Join(',', command.Tags),
            ["categories"] = string.Join(',', command.Categories)
        };
    }

    private sealed record SemanticParagraph(string Text, int StartPage, int EndPage, string Section, bool IsHeading, string? WorksheetName, int? SlideNumber, string? TableId, string? FormId)
    {
        public int EstimatedTokens { get; } = EstimateTokens(Text);
    }
}
