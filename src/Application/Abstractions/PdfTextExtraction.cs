using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Chatbot.Application.Abstractions;

public static partial class PdfTextExtraction
{
    [GeneratedRegex("\\((?<text>(?:\\\\.|[^\\\\()]){2,})\\)\\s*(?:Tj|TJ|'|\")", RegexOptions.Compiled)]
    private static partial Regex LiteralTextRegex();

    [GeneratedRegex(@"\[(?<items>.*?)\]\s*TJ", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex TextArrayRegex();

    [GeneratedRegex(@"\((?<text>(?:\\.|[^\\()]){2,})\)", RegexOptions.Compiled)]
    private static partial Regex ArrayItemRegex();

    [GeneratedRegex(@"^\s*%PDF-", RegexOptions.Compiled)]
    private static partial Regex PdfHeaderRegex();

    private static readonly string[] PdfNoiseMarkers =
    [
        "%PDF-",
        "endobj",
        "xref",
        "trailer",
        "/Type",
        "/Font",
        "/Subtype",
        "obj <<",
        "stream",
        "endstream"
    ];

    public static string? TryExtractText(byte[] bytes)
    {
        if (bytes.Length == 0 || !LooksLikePdf(bytes))
        {
            return null;
        }

        var candidates = new List<string>();
        foreach (var streamBytes in EnumerateStreams(bytes))
        {
            ExtractCandidateText(streamBytes, candidates);
        }

        if (candidates.Count == 0)
        {
            ExtractCandidateText(bytes, candidates);
        }

        var normalized = NormalizeCandidates(candidates);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    public static bool LooksLikeArtifactText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = NormalizeWhitespace(text);
        if (normalized.Length == 0)
        {
            return false;
        }

        var noiseHits = PdfNoiseMarkers.Count(marker => normalized.Contains(marker, StringComparison.OrdinalIgnoreCase));
        if (noiseHits == 0)
        {
            return false;
        }

        var letterRatio = normalized.Count(char.IsLetter) / (double)normalized.Length;
        return noiseHits >= 2 || letterRatio < 0.45;
    }

    private static bool LooksLikePdf(byte[] bytes)
    {
        var headerLength = Math.Min(bytes.Length, 16);
        var header = Encoding.Latin1.GetString(bytes, 0, headerLength);
        return PdfHeaderRegex().IsMatch(header);
    }

    private static IEnumerable<byte[]> EnumerateStreams(byte[] bytes)
    {
        var content = Encoding.Latin1.GetString(bytes);
        var searchIndex = 0;

        while (true)
        {
            var streamKeywordIndex = content.IndexOf("stream", searchIndex, StringComparison.Ordinal);
            if (streamKeywordIndex < 0)
            {
                yield break;
            }

            var startIndex = streamKeywordIndex + "stream".Length;
            if (startIndex < content.Length && content[startIndex] == '\r')
            {
                startIndex++;
            }

            if (startIndex < content.Length && content[startIndex] == '\n')
            {
                startIndex++;
            }

            var endIndex = content.IndexOf("endstream", startIndex, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                yield break;
            }

            var length = Math.Max(0, endIndex - startIndex);
            var slice = new byte[length];
            Array.Copy(bytes, startIndex, slice, 0, length);

            var dictionaryWindowStart = Math.Max(0, streamKeywordIndex - 256);
            var dictionaryWindow = content[dictionaryWindowStart..streamKeywordIndex];
            var decoded = dictionaryWindow.Contains("/FlateDecode", StringComparison.Ordinal)
                ? TryInflate(slice) ?? slice
                : slice;

            yield return decoded;
            searchIndex = endIndex + "endstream".Length;
        }
    }

    private static byte[]? TryInflate(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return null;
        }

        try
        {
            using var input = new MemoryStream(payload);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static void ExtractCandidateText(byte[] bytes, ICollection<string> candidates)
    {
        var content = Encoding.Latin1.GetString(bytes);

        foreach (Match match in LiteralTextRegex().Matches(content))
        {
            var decoded = DecodePdfString(match.Groups["text"].Value);
            if (!string.IsNullOrWhiteSpace(decoded))
            {
                candidates.Add(decoded);
            }
        }

        foreach (Match match in TextArrayRegex().Matches(content))
        {
            foreach (Match item in ArrayItemRegex().Matches(match.Groups["items"].Value))
            {
                var decoded = DecodePdfString(item.Groups["text"].Value);
                if (!string.IsNullOrWhiteSpace(decoded))
                {
                    candidates.Add(decoded);
                }
            }
        }
    }

    private static string NormalizeCandidates(IEnumerable<string> candidates)
    {
        var lines = candidates
            .Select(NormalizeWhitespace)
            .Where(line => line.Length >= 3)
            .Where(line => !LooksLikeArtifactText(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join('\n', lines).Trim();
    }

    private static string DecodePdfString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current != '\\')
            {
                builder.Append(current);
                continue;
            }

            if (index == value.Length - 1)
            {
                break;
            }

            var escaped = value[++index];
            switch (escaped)
            {
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case '(':
                case ')':
                case '\\':
                    builder.Append(escaped);
                    break;
                default:
                    if (escaped is >= '0' and <= '7')
                    {
                        var octal = new StringBuilder().Append(escaped);
                        while (index + 1 < value.Length && octal.Length < 3 && value[index + 1] is >= '0' and <= '7')
                        {
                            octal.Append(value[++index]);
                        }

                        builder.Append((char)Convert.ToInt32(octal.ToString(), 8));
                        break;
                    }

                    builder.Append(escaped);
                    break;
            }
        }

        return builder.ToString();
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(' ', value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}