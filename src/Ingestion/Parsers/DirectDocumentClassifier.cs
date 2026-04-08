using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Parsers;

internal sealed class DirectDocumentClassifier
{
    private static readonly HashSet<string> StructuredDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".html", ".htm", ".csv", ".tsv", ".json", ".xml", ".yaml", ".yml", ".log", ".ini", ".cfg", ".sql"
    };

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

    public bool IsPdf(string extension)
    {
        return string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsDocx(string extension)
    {
        return string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsTextPayload(string extension, string? contentType, ReadOnlySpan<byte> bytes)
    {
        return TextExtensions.Contains(extension) || IsTextContentType(contentType) || IsTextLike(bytes);
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
}