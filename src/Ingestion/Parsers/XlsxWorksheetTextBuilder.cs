using System.Text;

namespace Chatbot.Ingestion.Parsers;

internal sealed class XlsxWorksheetTextBuilder
{
    public string Build(string sheetName, IReadOnlyList<List<string>> rows)
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
}