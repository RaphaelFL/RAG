using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Parsers;

public sealed class DirectDocumentParser : IDocumentParser
{
    private static readonly HashSet<string> StructuredDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".html", ".htm", ".csv", ".tsv", ".json", ".xml", ".yaml", ".yml", ".log", ".ini", ".cfg", ".sql"
    };

    private static readonly Regex XmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);

    public bool CanParse(IngestDocumentCommand command)
    {
        var extension = Path.GetExtension(command.FileName);
        if (IsImageDocument(command))
        {
            return false;
        }

        return StructuredDocumentExtensions.Contains(extension)
            || TextExtensions.Contains(extension)
            || IsTextContentType(command.ContentType)
            || string.IsNullOrWhiteSpace(extension);
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

        using var ms = new MemoryStream();
        await command.Content.CopyToAsync(ms, ct);

        if (command.Content.CanSeek)
        {
            command.Content.Position = 0;
        }

        var extension = Path.GetExtension(command.FileName);
        var bytes = ms.ToArray();

        if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var extractedPdfText = PdfTextExtraction.TryExtractText(bytes);
            if (!string.IsNullOrWhiteSpace(extractedPdfText))
            {
                return new DocumentParseResultDto
                {
                    Text = extractedPdfText
                };
            }

            return null;
        }

        if (string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
        {
            var extractedDocxText = TryExtractDocxText(bytes);
            if (!string.IsNullOrWhiteSpace(extractedDocxText))
            {
                return new DocumentParseResultDto
                {
                    Text = extractedDocxText
                };
            }

            return null;
        }

        if (TextExtensions.Contains(extension) || IsTextContentType(command.ContentType) || IsTextLike(bytes))
        {
            var text = DecodeText(bytes) ?? ExtractPrintableText(bytes);
            return string.IsNullOrWhiteSpace(text)
                ? null
                : new DocumentParseResultDto
                {
                    Text = text
                };
        }

        return null;
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

    private static string? TryExtractDocxText(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var entry = archive.GetEntry("word/document.xml");
            if (entry is null)
            {
                return null;
            }

            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var xml = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(xml))
            {
                return null;
            }

            var text = xml
                .Replace("</w:p>", "\n", StringComparison.OrdinalIgnoreCase)
                .Replace("<w:br/>", "\n", StringComparison.OrdinalIgnoreCase)
                .Replace("<w:cr/>", "\n", StringComparison.OrdinalIgnoreCase)
                .Replace("<w:tab/>", "\t", StringComparison.OrdinalIgnoreCase);

            text = XmlTagRegex.Replace(text, " ");
            text = WebUtility.HtmlDecode(text);

            var normalized = string.Join('\n', text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))))
                .Trim();

            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsImageDocument(IngestDocumentCommand command)
    {
        var extension = Path.GetExtension(command.FileName);
        return extension is ".png" or ".jpg" or ".jpeg"
            || string.Equals(command.ContentType, "image/png", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/xml", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/yaml", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/x-yaml", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/sql", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("text/xml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextLike(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return false;
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return true;
        }

        if (bytes.Length >= 2)
        {
            var hasUtf16Bom = (bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF);
            if (hasUtf16Bom)
            {
                return true;
            }
        }

        var sampleLength = Math.Min(bytes.Length, 512);
        var printable = 0;
        var alphaNumeric = 0;

        for (var index = 0; index < sampleLength; index++)
        {
            var value = bytes[index];
            if (value == 0)
            {
                return false;
            }

            if (value is 9 or 10 or 13 || value is >= 32 and <= 126 || value >= 160)
            {
                printable++;
            }

            if ((value is >= (byte)'0' and <= (byte)'9')
                || (value is >= (byte)'A' and <= (byte)'Z')
                || (value is >= (byte)'a' and <= (byte)'z'))
            {
                alphaNumeric++;
            }
        }

        var printableRatio = printable / (double)sampleLength;
        return printableRatio >= 0.85 && (alphaNumeric > 0 || printable == sampleLength);
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