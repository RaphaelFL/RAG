using System.Xml.Linq;

namespace Chatbot.Ingestion.Parsers;

internal sealed class PptxSlideParagraphExtractor
{
    public List<string> Extract(XDocument slide)
    {
        return slide.Descendants()
            .Where(element => element.Name.LocalName == "p")
            .Select(paragraph => string.Concat(paragraph.Descendants().Where(node => node.Name.LocalName == "t").Select(node => node.Value)))
            .Select(NormalizeWhitespace)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}