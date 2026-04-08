using System.Text;

namespace Chatbot.Ingestion.Parsers;

internal sealed class DirectTextPayloadExtractor
{
    public string? Extract(byte[] bytes)
    {
        return DecodeText(bytes) ?? ExtractPrintableText(bytes);
    }

    private static string? DecodeText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd()
            .Trim('\uFEFF', '\0', ' ', '\r', '\n', '\t');

        return LooksLikeDecodedText(text) ? text : null;
    }

    private static string? ExtractPrintableText(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length);

        foreach (var value in bytes)
        {
            if (value is 9 or 10 or 13 || value is >= 32 and <= 126 || value >= 160)
            {
                builder.Append((char)value);
            }
        }

        var text = builder.ToString();
        var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();

        return normalized.Length >= 20 ? normalized : null;
    }

    private static bool LooksLikeDecodedText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var sample = text.Length > 512 ? text[..512] : text;
        var printable = sample.Count(character => !char.IsControl(character) || char.IsWhiteSpace(character));
        var printableRatio = printable / (double)sample.Length;
        return printableRatio >= 0.9;
    }
}