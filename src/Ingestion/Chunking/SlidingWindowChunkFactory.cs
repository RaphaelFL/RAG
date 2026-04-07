using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Chunking;

internal sealed class SlidingWindowChunkFactory
{
    public List<DocumentChunkIndexDto> Build(IngestDocumentCommand command, string sourceText, IReadOnlyList<SemanticParagraph> paragraphs, int maxTokens, int overlapTokens)
    {
        var chunks = new List<DocumentChunkIndexDto>();
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

        if (chunks.Count == 0)
        {
            chunks.Add(CreateFallbackChunk(command, sourceText));
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

    private static DocumentChunkIndexDto CreateChunk(IngestDocumentCommand command, IReadOnlyList<SemanticParagraph> paragraphs, int index)
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

    private static DocumentChunkIndexDto CreateFallbackChunk(IngestDocumentCommand command, string sourceText)
    {
        return new DocumentChunkIndexDto
        {
            ChunkId = $"{command.DocumentId:N}-chunk-0001",
            DocumentId = command.DocumentId,
            Content = sourceText,
            PageNumber = 1,
            Section = ResolveDocumentTitle(command),
            Metadata = BuildMetadata(command, ResolveDocumentTitle(command), 1, 1, EstimateTokens(sourceText))
        };
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

    private static int EstimateTokens(string text)
    {
        return Math.Max(1, (int)Math.Ceiling(text.Length / 4d));
    }

    private static string ResolveDocumentTitle(IngestDocumentCommand command)
    {
        return string.IsNullOrWhiteSpace(command.DocumentTitle) ? command.FileName : command.DocumentTitle;
    }
}