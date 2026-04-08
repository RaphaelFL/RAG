using System.Globalization;
using System.Text.Json;
using StackExchange.Redis;

namespace Chatbot.Infrastructure.Persistence;

internal static class RedisSearchResponseMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Dictionary<string, RedisSearchResultAccumulator> ParseSearchResponse(RedisResult response, bool isVectorResult)
    {
        var items = new Dictionary<string, RedisSearchResultAccumulator>(StringComparer.OrdinalIgnoreCase);
        if (response.IsNull)
        {
            return items;
        }

        var values = (RedisResult[])response!;
        if (values.Length <= 1)
        {
            return items;
        }

        var step = isVectorResult ? 2 : 3;
        for (var index = 1; index < values.Length; index += step)
        {
            var documentKey = values[index].ToString();
            RedisResult[] fieldEntries;
            double textualScore = 0;

            if (isVectorResult)
            {
                fieldEntries = (RedisResult[])values[index + 1]!;
            }
            else
            {
                textualScore = double.TryParse(values[index + 1].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedScore)
                    ? parsedScore
                    : 0;
                fieldEntries = (RedisResult[])values[index + 2]!;
            }

            var fields = ToFieldDictionary(fieldEntries);
            var chunkId = ReadField(fields, "chunkId");
            if (string.IsNullOrWhiteSpace(chunkId))
            {
                chunkId = ExtractChunkIdFromKey(documentKey);
            }

            if (!Guid.TryParse(ReadField(fields, "documentId"), out var documentId))
            {
                continue;
            }

            var metadataJson = ReadField(fields, "metadata", "{}");
            var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson, SerializerOptions)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!metadata.ContainsKey("title"))
            {
                metadata["title"] = ReadField(fields, "title");
            }

            if (!metadata.ContainsKey("documentTitle"))
            {
                metadata["documentTitle"] = ReadField(fields, "title");
            }

            if (!metadata.ContainsKey("section"))
            {
                metadata["section"] = ReadField(fields, "sectionTitle");
            }

            if (!metadata.ContainsKey("page"))
            {
                metadata["page"] = ReadField(fields, "pageNumber");
            }

            if (!metadata.ContainsKey("endPage"))
            {
                metadata["endPage"] = ReadField(fields, "endPageNumber");
            }

            items[chunkId] = new RedisSearchResultAccumulator
            {
                ChunkId = chunkId,
                DocumentId = documentId,
                Content = ReadField(fields, "content"),
                Metadata = metadata,
                VectorScore = isVectorResult ? NormalizeVectorScore(ReadField(fields, "vector_score")) : 0,
                LexicalScore = isVectorResult ? 0 : NormalizeLexicalScore(textualScore)
            };
        }

        return items;
    }

    private static Dictionary<string, string> ToFieldDictionary(RedisResult[] fieldEntries)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index + 1 < fieldEntries.Length; index += 2)
        {
            fields[fieldEntries[index].ToString()] = fieldEntries[index + 1].ToString();
        }

        return fields;
    }

    private static string ReadField(Dictionary<string, string> fields, string key, string fallback = "")
    {
        return fields.TryGetValue(key, out var value) ? value ?? fallback : fallback;
    }

    private static string ExtractChunkIdFromKey(string key)
    {
        var lastSeparator = key.LastIndexOf(':');
        return lastSeparator >= 0 && lastSeparator + 1 < key.Length
            ? key[(lastSeparator + 1)..]
            : key;
    }

    private static double NormalizeVectorScore(string rawDistance)
    {
        if (!double.TryParse(rawDistance, NumberStyles.Float, CultureInfo.InvariantCulture, out var distance))
        {
            return 0;
        }

        return Math.Round(1d / (1d + Math.Max(0d, distance)), 4);
    }

    private static double NormalizeLexicalScore(double score)
    {
        return Math.Round(Math.Max(0d, Math.Min(1d, score)), 4);
    }
}