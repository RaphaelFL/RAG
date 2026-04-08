using System.Xml.Linq;

namespace Chatbot.Ingestion.Parsers;

internal sealed class XlsxWorksheetRowsParser
{
    private readonly XlsxCellValueResolver _cellValueResolver;

    public XlsxWorksheetRowsParser(IReadOnlyList<string> sharedStrings)
    {
        _cellValueResolver = new XlsxCellValueResolver(sharedStrings);
    }

    public List<List<string>> Parse(XDocument worksheet)
    {
        var rows = new List<List<string>>();
        foreach (var row in worksheet.Descendants().Where(element => element.Name.LocalName == "row"))
        {
            var cells = new Dictionary<int, string>();
            foreach (var cell in row.Elements().Where(element => element.Name.LocalName == "c"))
            {
                var reference = cell.Attribute("r")?.Value ?? string.Empty;
                var columnIndex = _cellValueResolver.ResolveColumnIndex(reference);
                var value = _cellValueResolver.ResolveValue(cell);
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
}