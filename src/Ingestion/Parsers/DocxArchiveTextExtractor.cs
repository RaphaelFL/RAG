using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Chatbot.Ingestion.Parsers;

internal sealed class DocxArchiveTextExtractor
{
    private static readonly Regex XmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);

    public string? TryExtract(byte[] bytes)
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
}