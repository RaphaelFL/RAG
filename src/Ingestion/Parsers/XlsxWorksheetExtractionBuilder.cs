using System.Globalization;
using System.Text;
using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Parsers;

internal sealed class XlsxWorksheetExtractionBuilder
{
    public PageExtractionDto BuildPage(int pageNumber, string sheetName, IReadOnlyList<List<string>> rows, string worksheetText)
    {
        var tableId = BuildTableId(sheetName, rows);

        return new PageExtractionDto
        {
            PageNumber = pageNumber,
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
        };
    }

    public object BuildStructuredSheet(string sheetName, IReadOnlyList<List<string>> rows)
    {
        return new
        {
            name = sheetName,
            tableId = BuildTableId(sheetName, rows),
            rowCount = rows.Count,
            columnCount = rows.Max(row => row.Count),
            headers = rows.FirstOrDefault() ?? new List<string>(),
            rows
        };
    }

    private static string? BuildTableId(string sheetName, IReadOnlyList<List<string>> rows)
    {
        return rows.Count > 1 && rows.Any(row => row.Count > 1)
            ? $"{Slugify(sheetName)}-table-001"
            : null;
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
}