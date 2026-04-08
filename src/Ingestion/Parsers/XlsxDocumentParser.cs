using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Parsers;

public sealed class XlsxDocumentParser : IDocumentParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public bool CanParse(IngestDocumentCommand command)
    {
        return string.Equals(Path.GetExtension(command.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.ContentType, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase);
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
            var sharedStringsLoader = new XlsxSharedStringsLoader(xmlLoader);
            var relationshipTargetResolver = new OpenXmlRelationshipTargetResolver();
            var workbook = xmlLoader.LoadXml("xl/workbook.xml");
            var workbookRels = xmlLoader.LoadXml("xl/_rels/workbook.xml.rels");
            if (workbook is null || workbookRels is null)
            {
                return null;
            }

            var sharedStrings = sharedStringsLoader.Load();
            var relationshipTargets = relationshipTargetResolver.ResolveTargets(workbookRels, "xl");
            var worksheetRowsParser = new XlsxWorksheetRowsParser(sharedStrings);
            var worksheetTextBuilder = new XlsxWorksheetTextBuilder();
            var worksheetExtractionBuilder = new XlsxWorksheetExtractionBuilder();
            var sheets = workbook.Descendants().Where(element => element.Name.LocalName == "sheet").ToList();
            if (sheets.Count == 0)
            {
                return null;
            }

            var pages = new List<PageExtractionDto>();
            var structuredSheets = new List<object>();
            var semanticText = new StringBuilder();

            for (var index = 0; index < sheets.Count; index++)
            {
                var sheet = sheets[index];
                var sheetName = sheet.Attribute("name")?.Value ?? $"Planilha {index + 1}";
                var relationshipId = relationshipTargetResolver.ResolveRelationshipId(sheet);
                if (string.IsNullOrWhiteSpace(relationshipId)
                    || !relationshipTargets.TryGetValue(relationshipId, out var worksheetPath))
                {
                    continue;
                }

                var worksheet = xmlLoader.LoadXml(worksheetPath);
                if (worksheet is null)
                {
                    continue;
                }

                var rows = worksheetRowsParser.Parse(worksheet);
                if (rows.Count == 0)
                {
                    continue;
                }

                var worksheetText = worksheetTextBuilder.Build(sheetName, rows);
                pages.Add(worksheetExtractionBuilder.BuildPage(index + 1, sheetName, rows, worksheetText));
                structuredSheets.Add(worksheetExtractionBuilder.BuildStructuredSheet(sheetName, rows));

                if (semanticText.Length > 0)
                {
                    semanticText.AppendLine();
                    semanticText.AppendLine();
                }

                semanticText.Append(worksheetText);
            }

            if (pages.Count == 0)
            {
                return null;
            }

            var structuredJson = JsonSerializer.Serialize(new
            {
                kind = "workbook",
                fileName = command.FileName,
                worksheetCount = pages.Count,
                worksheets = structuredSheets
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