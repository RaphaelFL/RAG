using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Npgsql;
using NpgsqlTypes;
using Pgvector;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class PgVectorCommandParameterBinder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly VectorStoreOptions _options;

    public PgVectorCommandParameterBinder(VectorStoreOptions options)
    {
        _options = options;
    }

    public void FillUpsertParameters(NpgsqlCommand command, DocumentChunkIndexDto chunk)
    {
        var metadata = new Dictionary<string, string>(chunk.Metadata, StringComparer.OrdinalIgnoreCase);
        var tenantId = ResolveTenantId(metadata);
        var normalizedVector = NormalizeVectorDimensions(chunk.Embedding ?? Array.Empty<float>());
        var tags = SplitCsv(metadata, "tags");
        var categories = SplitCsv(metadata, "categories");

        command.Parameters.AddWithValue("tenant_id", NpgsqlDbType.Uuid, tenantId);
        command.Parameters.AddWithValue("document_id", NpgsqlDbType.Uuid, chunk.DocumentId);
        command.Parameters.AddWithValue("chunk_id", NpgsqlDbType.Text, chunk.ChunkId);
        command.Parameters.AddWithValue("chunk_index", NpgsqlDbType.Integer, ResolveChunkIndex(chunk, metadata));
        command.Parameters.AddWithValue("content", NpgsqlDbType.Text, chunk.Content);
        command.Parameters.AddWithValue("normalized_text", NpgsqlDbType.Text, chunk.Content);
        command.Parameters.AddWithValue("embedding", new Vector(normalizedVector));
        command.Parameters.AddWithValue("metadata", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(metadata, SerializerOptions));
        command.Parameters.AddWithValue("title", NpgsqlDbType.Text, ReadMetadata(metadata, "title"));
        command.Parameters.AddWithValue("source_name", NpgsqlDbType.Text, ReadMetadata(metadata, "sourceName"));
        command.Parameters.AddWithValue("source_type", NpgsqlDbType.Text, ReadMetadata(metadata, "sourceType"));
        command.Parameters.AddWithValue("content_type", NpgsqlDbType.Text, ReadMetadata(metadata, "contentType"));
        command.Parameters.AddWithValue("section_title", NpgsqlDbType.Text, chunk.Section ?? ReadMetadata(metadata, "section"));
        command.Parameters.AddWithValue("page_number", NpgsqlDbType.Integer, chunk.PageNumber);
        command.Parameters.AddWithValue("end_page_number", NpgsqlDbType.Integer, ParseInt(ReadMetadata(metadata, "endPage"), chunk.PageNumber));
        command.Parameters.AddWithValue("access_policy", NpgsqlDbType.Text, ReadMetadata(metadata, "accessPolicy"));
        command.Parameters.AddWithValue("tags", NpgsqlDbType.Array | NpgsqlDbType.Text, tags);
        command.Parameters.AddWithValue("categories", NpgsqlDbType.Array | NpgsqlDbType.Text, categories);
        command.Parameters.AddWithValue("content_hash", NpgsqlDbType.Text, ReadMetadata(metadata, "contentHash"));
        command.Parameters.AddWithValue("embedding_model_name", NpgsqlDbType.Text, ReadMetadata(metadata, "embeddingModel", string.Empty));
        command.Parameters.AddWithValue("embedding_model_version", NpgsqlDbType.Text, ReadMetadata(metadata, "embeddingModelVersion", string.Empty));
        command.Parameters.AddWithValue("vector_dimensions", NpgsqlDbType.Integer, normalizedVector.Length);
    }

    public void FillSearchParameters(NpgsqlCommand command, string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters)
    {
        command.Parameters.AddWithValue("query_text", NpgsqlDbType.Text, query ?? string.Empty);
        command.Parameters.AddWithValue("top_k", NpgsqlDbType.Integer, Math.Max(1, topK));
        command.Parameters.AddWithValue("tenant_id", NpgsqlDbType.Uuid, filters?.TenantId ?? Guid.Empty);
        command.Parameters.AddWithValue("has_query_embedding", NpgsqlDbType.Boolean, queryEmbedding is { Length: > 0 });
        command.Parameters.AddWithValue("query_embedding", queryEmbedding is { Length: > 0 }
            ? new Vector(NormalizeVectorDimensions(queryEmbedding))
            : DBNull.Value);

        AddNullableArrayParameter(command, "document_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, filters?.DocumentIds?.ToArray());
        AddNullableArrayParameter(command, "tags", NpgsqlDbType.Array | NpgsqlDbType.Text, filters?.Tags?.ToArray());
        AddNullableArrayParameter(command, "categories", NpgsqlDbType.Array | NpgsqlDbType.Text, filters?.Categories?.ToArray());
        AddNullableArrayParameter(command, "content_types", NpgsqlDbType.Array | NpgsqlDbType.Text, filters?.ContentTypes?.ToArray());
        AddNullableArrayParameter(command, "sources", NpgsqlDbType.Array | NpgsqlDbType.Text, filters?.Sources?.ToArray());
    }

    private static void AddNullableArrayParameter<T>(NpgsqlCommand command, string name, NpgsqlDbType dbType, T[]? values)
    {
        if (values is { Length: > 0 })
        {
            command.Parameters.AddWithValue(name, dbType, values);
            return;
        }

        var parameter = command.Parameters.Add(name, dbType);
        parameter.Value = DBNull.Value;
    }

    private Guid ResolveTenantId(Dictionary<string, string> metadata)
    {
        var rawTenantId = ReadMetadata(metadata, "tenantId");
        return Guid.TryParse(rawTenantId, out var tenantId)
            ? tenantId
            : Guid.Empty;
    }

    private int ResolveChunkIndex(DocumentChunkIndexDto chunk, Dictionary<string, string> metadata)
    {
        if (metadata.TryGetValue("chunkIndex", out var rawIndex) && int.TryParse(rawIndex, out var chunkIndex))
        {
            return chunkIndex;
        }

        var suffix = chunk.ChunkId.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        return int.TryParse(suffix, out chunkIndex) ? chunkIndex : 0;
    }

    private float[] NormalizeVectorDimensions(float[] vector)
    {
        if (vector.Length == _options.Dimensions)
        {
            return vector;
        }

        var normalized = new float[_options.Dimensions];
        Array.Copy(vector, normalized, Math.Min(vector.Length, normalized.Length));
        return normalized;
    }

    private static string[] SplitCsv(Dictionary<string, string> metadata, string key)
    {
        return ReadMetadata(metadata, key)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int ParseInt(string? rawValue, int fallback)
    {
        return int.TryParse(rawValue, out var parsed) ? parsed : fallback;
    }

    private static string ReadMetadata(Dictionary<string, string> metadata, string key, string fallback = "")
    {
        return metadata.TryGetValue(key, out var value) ? value ?? fallback : fallback;
    }
}