using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
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
            var presentation = LoadXml(archive, "ppt/presentation.xml");
            var presentationRels = LoadXml(archive, "ppt/_rels/presentation.xml.rels");
            if (presentation is null || presentationRels is null)
            {
                return null;
            }

            var relationshipTargets = LoadRelationshipTargets(presentationRels, "ppt");
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
                var relationshipId = ResolveRelationshipId(slideId);
                if (string.IsNullOrWhiteSpace(relationshipId)
                    || !relationshipTargets.TryGetValue(relationshipId, out var slidePath))
                {
                    continue;
                }

                var slide = LoadXml(archive, slidePath);
                if (slide is null)
                {
                    continue;
                }

                var slideTexts = ExtractSlideParagraphs(slide);
                if (slideTexts.Count == 0)
                {
                    continue;
                }

                var notesText = TryExtractSlideNotes(archive, slidePath);
                var title = slideTexts.FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)) ?? $"Slide {index + 1}";
                var slideText = BuildSlideText(index + 1, title, slideTexts, notesText);

                pages.Add(new PageExtractionDto
                {
                    PageNumber = index + 1,
                    SlideNumber = index + 1,
                    SectionTitle = title,
                    Text = slideText,
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["slideNumber"] = (index + 1).ToString(),
                        ["title"] = title,
                        ["hasNotes"] = (!string.IsNullOrWhiteSpace(notesText)).ToString()
                    }
                });

                structuredSlides.Add(new
                {
                    slideNumber = index + 1,
                    title,
                    bullets = slideTexts,
                    notes = notesText
                });

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

    private static XDocument? LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path.Replace('\\', '/'));
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static Dictionary<string, string> LoadRelationshipTargets(XDocument relationshipsDocument, string baseDirectory)
    {
        return relationshipsDocument.Descendants()
            .Where(element => element.Name.LocalName == "Relationship")
            .Select(element => new
            {
                Id = element.Attribute("Id")?.Value,
                Target = element.Attribute("Target")?.Value
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Target))
            .ToDictionary(
                item => item.Id!,
                item => ResolveZipPath(baseDirectory, item.Target!),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveZipPath(string baseDirectory, string target)
    {
        var normalizedBase = baseDirectory.Replace('\\', '/').Trim('/');
        var combined = target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : string.Join('/', new[] { normalizedBase, target }.Where(segment => !string.IsNullOrWhiteSpace(segment)));

        var parts = new Stack<string>();
        foreach (var segment in combined.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (parts.Count > 0)
                {
                    parts.Pop();
                }

                continue;
            }

            parts.Push(segment);
        }

        return string.Join('/', parts.Reverse());
    }

    private static List<string> ExtractSlideParagraphs(XDocument slide)
    {
        return slide.Descendants()
            .Where(element => element.Name.LocalName == "p")
            .Select(paragraph => string.Concat(paragraph.Descendants().Where(node => node.Name.LocalName == "t").Select(node => node.Value)))
            .Select(NormalizeInlineWhitespace)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    private static string TryExtractSlideNotes(ZipArchive archive, string slidePath)
    {
        var slideFileName = Path.GetFileName(slidePath);
        if (string.IsNullOrWhiteSpace(slideFileName))
        {
            return string.Empty;
        }

        var slideRelationshipsPath = $"ppt/slides/_rels/{slideFileName}.rels";
        var slideRels = LoadXml(archive, slideRelationshipsPath);
        if (slideRels is null)
        {
            return string.Empty;
        }

        var notesTarget = slideRels.Descendants()
            .Where(element => element.Name.LocalName == "Relationship")
            .FirstOrDefault(element => string.Equals(element.Attribute("Type")?.Value?.Split('/').LastOrDefault(), "notesSlide", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("Target")?.Value;

        if (string.IsNullOrWhiteSpace(notesTarget))
        {
            return string.Empty;
        }

        var notesPath = ResolveZipPath("ppt/slides", notesTarget);
        var notes = LoadXml(archive, notesPath);
        if (notes is null)
        {
            return string.Empty;
        }

        return string.Join('\n', notes.Descendants()
            .Where(element => element.Name.LocalName == "t")
            .Select(element => NormalizeInlineWhitespace(element.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string BuildSlideText(int slideNumber, string title, IReadOnlyList<string> paragraphs, string notesText)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Slide {slideNumber}: {title}");

        foreach (var paragraph in paragraphs)
        {
            builder.AppendLine(paragraph);
        }

        if (!string.IsNullOrWhiteSpace(notesText))
        {
            builder.AppendLine();
            builder.AppendLine("Speaker notes:");
            builder.AppendLine(notesText);
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeInlineWhitespace(string value)
    {
        return string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string? ResolveRelationshipId(XElement element)
    {
        return element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == "id" && attribute.Name.NamespaceName.Contains("relationships", StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }
}