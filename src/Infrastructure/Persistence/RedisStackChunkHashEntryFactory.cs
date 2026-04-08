using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using StackExchange.Redis;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisStackChunkHashEntryFactory
{
    private readonly VectorStoreOptions _vectorOptions;

    public RedisStackChunkHashEntryFactory(VectorStoreOptions vectorOptions)
    {
        _vectorOptions = vectorOptions;
    }

    public string BuildKey(string tenantId, string chunkId)
    {
        return $"{_vectorOptions.KeyPrefix}{tenantId}:{chunkId}";
    }

    public HashEntry[] Build(DocumentChunkIndexDto chunk)
    {
        var metadata = new Dictionary<string, string>(chunk.Metadata, StringComparer.OrdinalIgnoreCase);
        var tenantId = ReadMetadata(metadata, "tenantId");
        return new HashEntry[]
        {
            new("chunkId", chunk.ChunkId),
            new("tenantId", tenantId),
            new("documentId", chunk.DocumentId.ToString()),
            new("chunkIndex", ReadMetadata(metadata, "chunkIndex", ResolveChunkIndex(chunk.ChunkId))),
            new("content", chunk.Content),
            new("title", ReadMetadata(metadata, "title")),
            new("sourceName", ReadMetadata(metadata, "sourceName", ReadMetadata(metadata, "originalFileName"))),
            new("sourceType", ReadMetadata(metadata, "sourceType", ReadMetadata(metadata, "source"))),
            new("contentType", ReadMetadata(metadata, "contentType")),
            new("sectionTitle", chunk.Section ?? ReadMetadata(metadata, "section")),
            new("pageNumber", chunk.PageNumber.ToString(CultureInfo.InvariantCulture)),
            new("endPageNumber", ReadMetadata(metadata, "endPage", chunk.PageNumber.ToString(CultureInfo.InvariantCulture))),
            new("tags", ToTagValue(ReadMetadata(metadata, "tags"))),
            new("categories", ToTagValue(ReadMetadata(metadata, "categories"))),
            new("accessPolicy", ReadMetadata(metadata, "accessPolicy")),
            new("contentHash", ReadMetadata(metadata, "contentHash")),
            new("metadata", JsonSerializer.Serialize(metadata, OperationalAuditJsonSerializer.Options)),
            new("vector", EncodeVector(chunk.Embedding ?? Array.Empty<float>()))
        };
    }

    public string ResolveTenantId(DocumentChunkIndexDto chunk)
    {
        return ReadMetadata(chunk.Metadata, "tenantId");
    }

    private static string ToTagValue(string csv)
    {
        return string.Join('|', csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string ResolveChunkIndex(string chunkId)
    {
        var suffix = chunkId.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        return int.TryParse(suffix, out var parsed) ? parsed.ToString(CultureInfo.InvariantCulture) : "0";
    }

    private static byte[] EncodeVector(float[] vector)
    {
        var payload = new byte[vector.Length * sizeof(float)];
        for (var index = 0; index < vector.Length; index++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(index * sizeof(float), sizeof(float)), vector[index]);
        }

        return payload;
    }

    private static string ReadMetadata(IReadOnlyDictionary<string, string> metadata, string key, string fallback = "")
    {
        return metadata.TryGetValue(key, out var value) ? value ?? fallback : fallback;
    }
}