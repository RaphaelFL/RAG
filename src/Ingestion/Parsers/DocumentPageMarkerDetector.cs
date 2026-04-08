using System.Text.RegularExpressions;

namespace Chatbot.Ingestion.Parsers;

internal sealed class DocumentPageMarkerDetector
{
    private static readonly Regex PageMarkerRegex = new(@"^\s*(?:page|pagina|pg\.?)(?:\s+|\s*#\s*)(\d+)(?:\s*(?:/|de|of)\s*\d+)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PageNumberOnlyRegex = new(@"^\s*\d+\s*$", RegexOptions.Compiled);

    public bool TryParseStandalonePageMarker(string line, out int pageNumber)
    {
        var match = PageMarkerRegex.Match(line);
        if (match.Success && int.TryParse(match.Groups[1].Value, out pageNumber))
        {
            return true;
        }

        pageNumber = 0;
        return false;
    }

    public bool IsHeaderFooterCandidate(string line)
    {
        return line.Length is >= 3 and <= 120
            && line.Any(char.IsLetter)
            && !IsNoiseLine(line);
    }

    public bool IsNoiseLine(string line)
    {
        return PageNumberOnlyRegex.IsMatch(line)
            || TryParseStandalonePageMarker(line, out _)
            || line.Equals("confidencial", StringComparison.OrdinalIgnoreCase)
            || line.Equals("confidential", StringComparison.OrdinalIgnoreCase);
    }
}