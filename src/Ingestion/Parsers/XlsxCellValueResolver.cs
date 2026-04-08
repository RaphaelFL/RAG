using System.Globalization;
using System.Xml.Linq;

namespace Chatbot.Ingestion.Parsers;

internal sealed class XlsxCellValueResolver
{
    private readonly IReadOnlyList<string> _sharedStrings;

    public XlsxCellValueResolver(IReadOnlyList<string> sharedStrings)
    {
        _sharedStrings = sharedStrings;
    }

    public int ResolveColumnIndex(string reference)
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

    public string ResolveValue(XElement cell)
    {
        var type = cell.Attribute("t")?.Value;
        var rawValue = cell.Descendants().FirstOrDefault(element => element.Name.LocalName == "v")?.Value;
        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeWhitespace(string.Concat(cell.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value)));
        }

        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedStringIndex)
            && sharedStringIndex >= 0
            && sharedStringIndex < _sharedStrings.Count)
        {
            return NormalizeWhitespace(_sharedStrings[sharedStringIndex]);
        }

        return NormalizeWhitespace(rawValue ?? string.Empty);
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}