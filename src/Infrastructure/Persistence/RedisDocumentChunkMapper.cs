using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using StackExchange.Redis;

namespace Chatbot.Infrastructure.Persistence;

internal static class RedisDocumentChunkMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static DocumentChunkIndexDto? ToChunk(HashEntry[] entries)
    {
        if (entries.Length == 0)
        {
            return null;
        }

        var fields = entries.ToDictionary(entry => entry.Name.ToString(), entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        if (!Guid.TryParse(fields.TryGetValue("documentId", out var rawDocumentId) ? rawDocumentId.ToString() : null, out var documentId))
        {
            return null;
        }

        var metadata = fields.TryGetValue("metadata", out var rawMetadata)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(rawMetadata.ToString(), SerializerOptions)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var pageNumber = fields.TryGetValue("pageNumber", out var rawPageNumber)
            && int.TryParse(rawPageNumber.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPageNumber)
                ? parsedPageNumber
                : 0;

        var embedding = fields.TryGetValue("vector", out var rawVector) && !rawVector.IsNull
            ? DecodeVector((byte[])rawVector!)
            : null;

        return new DocumentChunkIndexDto
        {
            ChunkId = fields.TryGetValue("chunkId", out var rawChunkId) ? rawChunkId.ToString() : string.Empty,
            DocumentId = documentId,
            Content = fields.TryGetValue("content", out var rawContent) ? rawContent.ToString() : string.Empty,
            Embedding = embedding,
            PageNumber = pageNumber,
            Section = fields.TryGetValue("sectionTitle", out var rawSection) ? rawSection.ToString() : null,
            Metadata = metadata
        };
    }

    private static float[]? DecodeVector(byte[] payload)
    {
        if (payload.Length == 0 || payload.Length % sizeof(float) != 0)
        {
            return null;
        }

        var vector = new float[payload.Length / sizeof(float)];
        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] = BinaryPrimitives.ReadSingleLittleEndian(payload.AsSpan(index * sizeof(float), sizeof(float)));
        }

        return vector;
    }
}