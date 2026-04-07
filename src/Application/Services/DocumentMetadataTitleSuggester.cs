using System.Globalization;
using System.Text;

namespace Chatbot.Application.Services;

public sealed class DocumentMetadataTitleSuggester : IDocumentMetadataTitleSuggester
{
    public string Suggest(string fileName, string extractedText)
    {
        foreach (var line in DocumentMetadataTextUtility.NormalizeLineEndings(extractedText).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(8))
        {
            var normalizedLine = DocumentMetadataTextUtility.NormalizeWhitespace(line);
            if (!LooksLikeTitle(normalizedLine))
            {
                continue;
            }

            return NormalizeTitle(normalizedLine);
        }

        return HumanizeFileName(fileName);
    }

    private static bool LooksLikeTitle(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length is < 4 or > 120)
        {
            return false;
        }

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length <= 14 && line.Count(char.IsLetter) >= 4;
    }

    private static string NormalizeTitle(string title)
    {
        var normalized = title.Trim(' ', '-', '_', ':', '.', ';');
        var textInfo = CultureInfo.GetCultureInfo("pt-BR").TextInfo;

        return normalized.Equals(normalized.ToUpperInvariant(), StringComparison.Ordinal)
            ? textInfo.ToTitleCase(normalized.ToLowerInvariant())
            : normalized;
    }

    private static string HumanizeFileName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var builder = new StringBuilder(stem.Length);

        foreach (var character in stem)
        {
            builder.Append(character is '-' or '_' or '.' ? ' ' : character);
        }

        var normalized = DocumentMetadataTextUtility.NormalizeWhitespace(builder.ToString());
        var textInfo = CultureInfo.GetCultureInfo("pt-BR").TextInfo;
        return textInfo.ToTitleCase(normalized.ToLowerInvariant());
    }
}