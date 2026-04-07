using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
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
            var workbook = LoadXml(archive, "xl/workbook.xml");
            var workbookRels = LoadXml(archive, "xl/_rels/workbook.xml.rels");
            if (workbook is null || workbookRels is null)
            {
                return null;
            }

            var sharedStrings = LoadSharedStrings(archive);
            var relationshipTargets = LoadRelationshipTargets(workbookRels, "xl");
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
                var relationshipId = ResolveRelationshipId(sheet);
                if (string.IsNullOrWhiteSpace(relationshipId)
                    || !relationshipTargets.TryGetValue(relationshipId, out var worksheetPath))
                {
                    continue;
                }

                var worksheet = LoadXml(archive, worksheetPath);
                if (worksheet is null)
                {
                    continue;
                }

                var rows = ParseWorksheetRows(worksheet, sharedStrings);
                if (rows.Count == 0)
                {
                    continue;
                }

                var worksheetText = BuildWorksheetText(sheetName, rows);
                var tableId = rows.Count > 1 && rows.Any(row => row.Count > 1)
                    ? $"{Slugify(sheetName)}-table-001"
                    : null;

                pages.Add(new PageExtractionDto
                {
                    PageNumber = index + 1,
                    WorksheetName = sheetName,
                    SectionTitle = sheetName,
                    TableId = tableId,
                    Text = worksheetText,
                    Tables = tableId is null
                        ? null
                        : new List<TableDto>
                        {
                            new()
                            {
                                Rows = rows.Select(row => row.ToList()).ToList()
                            }
                        },
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["worksheetName"] = sheetName,
                        ["tableId"] = tableId ?? string.Empty,
                        ["rowCount"] = rows.Count.ToString(CultureInfo.InvariantCulture),
                        ["columnCount"] = rows.Max(row => row.Count).ToString(CultureInfo.InvariantCulture)
                    }
                });

                structuredSheets.Add(new
                {
                    name = sheetName,
                    tableId,
                    rowCount = rows.Count,
                    columnCount = rows.Max(row => row.Count),
                    headers = rows.FirstOrDefault() ?? new List<string>(),
                    rows
                });

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

    private static List<string> LoadSharedStrings(ZipArchive archive)
    {
        var document = LoadXml(archive, "xl/sharedStrings.xml");
        if (document is null)
        {
            return new List<string>();
        }

        return document.Descendants()
            .Where(element => element.Name.LocalName == "si")
            .Select(item => string.Concat(item.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value)))
            .ToList();
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

    private static List<List<string>> ParseWorksheetRows(XDocument worksheet, IReadOnlyList<string> sharedStrings)
    {
        var rows = new List<List<string>>();
        foreach (var row in worksheet.Descendants().Where(element => element.Name.LocalName == "row"))
        {
            var cells = new Dictionary<int, string>();
            foreach (var cell in row.Elements().Where(element => element.Name.LocalName == "c"))
            {
                var reference = cell.Attribute("r")?.Value ?? string.Empty;
                var columnIndex = ResolveColumnIndex(reference);
                var value = ResolveCellValue(cell, sharedStrings);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                cells[columnIndex] = value;
            }

            if (cells.Count == 0)
            {
                continue;
            }

            var maxIndex = cells.Keys.Max();
            var normalized = new List<string>(Enumerable.Repeat(string.Empty, maxIndex + 1));
            foreach (var entry in cells)
            {
                normalized[entry.Key] = entry.Value;
            }

            while (normalized.Count > 0 && string.IsNullOrWhiteSpace(normalized[^1]))
            {
                normalized.RemoveAt(normalized.Count - 1);
            }

            if (normalized.Count > 0)
            {
                rows.Add(normalized);
            }
        }

        return rows;
    }

    private static int ResolveColumnIndex(string reference)
    {
        var letters = new string(reference.TakeWhile(character => char.IsLetter(character)).ToArray()).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(letters))
        {
            return 0;
        }

        var sum = 0;
        foreach (var letter in letters)
        {
            sum = (sum * 26) + (letter - 'A' + 1);
        }

        return Math.Max(0, sum - 1);
    }

    private static string ResolveCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value;
        var rawValue = cell.Descendants().FirstOrDefault(element => element.Name.LocalName == "v")?.Value;
        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeInlineWhitespace(string.Concat(cell.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value)));
        }

        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedStringIndex)
            && sharedStringIndex >= 0
            && sharedStringIndex < sharedStrings.Count)
        {
            return NormalizeInlineWhitespace(sharedStrings[sharedStringIndex]);
        }

        return NormalizeInlineWhitespace(rawValue ?? string.Empty);
    }

    private static string BuildWorksheetText(string sheetName, IReadOnlyList<List<string>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Worksheet: {sheetName}");

        var headers = rows.FirstOrDefault() ?? new List<string>();
        if (rows.Count > 1 && headers.Count > 1 && headers.All(header => !string.IsNullOrWhiteSpace(header)))
        {
            builder.AppendLine($"Columns: {string.Join(", ", headers)}");
            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var pairs = headers.Select((header, index) => new
                    {
                        Header = header,
                        Value = index < row.Count ? row[index] : string.Empty
                    })
                    .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                    .Select(item => $"{item.Header}: {item.Value}");

                var line = string.Join(" | ", pairs);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    builder.AppendLine(line);
                }
            }
        }
        else
        {
            foreach (var row in rows)
            {
                var line = string.Join(" | ", row.Where(value => !string.IsNullOrWhiteSpace(value)));
                if (!string.IsNullOrWhiteSpace(line))
                {
                    builder.AppendLine(line);
                }
            }
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeInlineWhitespace(string value)
    {
        return string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        return builder.ToString().Trim('-');
    }

    private static string? ResolveRelationshipId(XElement element)
    {
        return element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == "id" && attribute.Name.NamespaceName.Contains("relationships", StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }
}