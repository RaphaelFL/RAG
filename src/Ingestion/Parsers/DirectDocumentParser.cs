using System.Text;
using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Parsers;

public sealed class DirectDocumentParser : IDocumentParser
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".txt", ".md", ".html", ".htm"
    };

    private static readonly HashSet<string> TextFirstExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".html", ".htm"
    };

    public bool CanParse(IngestDocumentCommand command)
    {
        var extension = Path.GetExtension(command.FileName);
        return !string.IsNullOrWhiteSpace(extension) && SupportedExtensions.Contains(extension);
    }

    public async Task<string?> ParseAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        if (!CanParse(command))
        {
            return null;
        }

        if (command.Content.CanSeek)
        {
            command.Content.Position = 0;
        }

        using var ms = new MemoryStream();
        await command.Content.CopyToAsync(ms, ct);

        if (command.Content.CanSeek)
        {
            command.Content.Position = 0;
        }

        var extension = Path.GetExtension(command.FileName);
        var bytes = ms.ToArray();

        return TextFirstExtensions.Contains(extension)
            ? DecodeUtf8(bytes)
            : ExtractPrintableText(bytes);
    }

    private static string? DecodeUtf8(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes)
            .Trim('\uFEFF', '\0', ' ', '\r', '\n', '\t');

        return string.IsNullOrWhiteSpace(text) ? null : text;
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
}