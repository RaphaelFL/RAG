using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Parsers;

public sealed class PptxDocumentParser : IDocumentParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public bool CanParse(IngestDocumentCommand command)
    {
        return string.Equals(Path.GetExtension(command.FileName), ".pptx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.ContentType, "application/vnd.openxmlformats-officedocument.presentationml.presentation", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DocumentParseResultDto?> ParseAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        if (!CanParse(command))
        {
            return null;
        }

        if (command.Content.CanSeek)
        {
            command.Content.Position = 0;
        }

        using var buffer = new MemoryStream();
        await command.Content.CopyToAsync(buffer, ct);

        if (command.Content.CanSeek)
        {
            command.Content.Position = 0;
        }

        try
        {
            using var archiveStream = new MemoryStream(buffer.ToArray(), writable: false);
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);
            var xmlLoader = new OpenXmlArchiveXmlLoader(archive);
            var relationshipTargetResolver = new OpenXmlRelationshipTargetResolver();
            var slideParagraphExtractor = new PptxSlideParagraphExtractor();
            var slideNotesExtractor = new PptxSlideNotesExtractor(xmlLoader, relationshipTargetResolver);
            var slideTextBuilder = new PptxSlideTextBuilder();
            var slideExtractionBuilder = new PptxSlideExtractionBuilder();
            var presentation = xmlLoader.LoadXml("ppt/presentation.xml");
            var presentationRels = xmlLoader.LoadXml("ppt/_rels/presentation.xml.rels");
            if (presentation is null || presentationRels is null)
            {
                return null;
            }

            var relationshipTargets = relationshipTargetResolver.ResolveTargets(presentationRels, "ppt");
            var slideIds = presentation.Descendants().Where(element => element.Name.LocalName == "sldId").ToList();
            if (slideIds.Count == 0)
            {
                return null;
            }

            var pages = new List<PageExtractionDto>();
            var structuredSlides = new List<object>();
            var semanticText = new StringBuilder();

            for (var index = 0; index < slideIds.Count; index++)
            {
                var slideId = slideIds[index];
                var relationshipId = relationshipTargetResolver.ResolveRelationshipId(slideId);
                if (string.IsNullOrWhiteSpace(relationshipId)
                    || !relationshipTargets.TryGetValue(relationshipId, out var slidePath))
                {
                    continue;
                }

                var slide = xmlLoader.LoadXml(slidePath);
                if (slide is null)
                {
                    continue;
                }

                var slideTexts = slideParagraphExtractor.Extract(slide);
                if (slideTexts.Count == 0)
                {
                    continue;
                }

                var notesText = slideNotesExtractor.TryExtract(slidePath);
                var title = slideTexts.FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)) ?? $"Slide {index + 1}";
                var slideText = slideTextBuilder.Build(index + 1, title, slideTexts, notesText);

                pages.Add(slideExtractionBuilder.BuildPage(index + 1, title, slideText, notesText));
                structuredSlides.Add(slideExtractionBuilder.BuildStructuredSlide(index + 1, title, slideTexts, notesText));

                if (semanticText.Length > 0)
                {
                    semanticText.AppendLine();
                    semanticText.AppendLine();
                }

                semanticText.Append(slideText);
            }

            if (pages.Count == 0)
            {
                return null;
            }

            var structuredJson = JsonSerializer.Serialize(new
            {
                kind = "presentation",
                fileName = command.FileName,
                slideCount = pages.Count,
                slides = structuredSlides
            }, SerializerOptions);

            return new DocumentParseResultDto
            {
                Text = semanticText.ToString(),
                StructuredJson = structuredJson,
                Pages = pages
            };
        }
        catch
        {
            return null;
        }
    }
}