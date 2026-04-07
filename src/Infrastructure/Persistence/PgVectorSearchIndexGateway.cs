using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Pgvector.Npgsql;

namespace Chatbot.Infrastructure.Persistence;

public sealed class PgVectorSearchIndexGateway : ISearchIndexGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly VectorStoreOptions _options;
    private readonly ILogger<PgVectorSearchIndexGateway> _logger;
    private readonly ISearchIndexGateway _fallbackGateway;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    private NpgsqlDataSource? _dataSource;
    private bool _pgVectorUnavailable;

    public PgVectorSearchIndexGateway(
        IOptions<VectorStoreOptions> options,
        ILogger<PgVectorSearchIndexGateway> logger,
        ISearchIndexGateway fallbackGateway)
    {
        _options = options.Value;
        _logger = logger;
        _fallbackGateway = fallbackGateway;
    }

    public async Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        var dataSource = await GetDataSourceAsync(ct);
        if (dataSource is null)
        {
            await _fallbackGateway.IndexDocumentChunksAsync(chunks, ct);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);

            foreach (var chunk in chunks)
            {
                if (chunk.Embedding is not { Length: > 0 })
                {
                    continue;
                }

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $@"
INSERT INTO {GetQualifiedTableName()} (
    tenant_id,
    document_id,
    chunk_id,
    chunk_index,
    content,
    normalized_text,
    embedding,
    metadata,
    title,
    source_name,
    source_type,
    content_type,
    section_title,
    page_number,
    end_page_number,
    access_policy,
    tags,
    categories,
    content_hash,
    embedding_model_name,
    embedding_model_version,
    vector_dimensions,
    updated_at_utc
) VALUES (
    @tenant_id,
    @document_id,
    @chunk_id,
    @chunk_index,
    @content,
    @normalized_text,
    @embedding,
    @metadata::jsonb,
    @title,
    @source_name,
    @source_type,
    @content_type,
    @section_title,
    @page_number,
    @end_page_number,
    @access_policy,
    @tags,
    @categories,
    @content_hash,
    @embedding_model_name,
    @embedding_model_version,
    @vector_dimensions,
    now()
)
ON CONFLICT (tenant_id, chunk_id)
DO UPDATE SET
    document_id = excluded.document_id,
    chunk_index = excluded.chunk_index,
    content = excluded.content,
    normalized_text = excluded.normalized_text,
    embedding = excluded.embedding,
    metadata = excluded.metadata,
    title = excluded.title,
    source_name = excluded.source_name,
    source_type = excluded.source_type,
    content_type = excluded.content_type,
    section_title = excluded.section_title,
    page_number = excluded.page_number,
    end_page_number = excluded.end_page_number,
    access_policy = excluded.access_policy,
    tags = excluded.tags,
    categories = excluded.categories,
    content_hash = excluded.content_hash,
    embedding_model_name = excluded.embedding_model_name,
    embedding_model_version = excluded.embedding_model_version,
    vector_dimensions = excluded.vector_dimensions,
    updated_at_utc = now();";

                FillUpsertParameters(command, chunk);
                await command.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            await _fallbackGateway.IndexDocumentChunksAsync(chunks, ct);
            ChatbotTelemetry.VectorStoreLatencyMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                new KeyValuePair<string, object?>("vector.operation", "upsert"),
                new KeyValuePair<string, object?>("vector.provider", "pgvector"));
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            await _fallbackGateway.IndexDocumentChunksAsync(chunks, ct);
        }
    }

    public async Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        var dataSource = await GetDataSourceAsync(ct);
        if (dataSource is null)
        {
            return await _fallbackGateway.HybridSearchAsync(query, queryEmbedding, topK, filters, ct);
        }

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = BuildSearchSql(query, queryEmbedding, filters);
            FillSearchParameters(command, query, queryEmbedding, topK, filters);

            var results = new List<SearchResultDto>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var metadataJson = reader.GetString(reader.GetOrdinal("metadata"));
                var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson, SerializerOptions)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                results.Add(new SearchResultDto
                {
                    ChunkId = reader.GetString(reader.GetOrdinal("chunk_id")),
                    DocumentId = reader.GetGuid(reader.GetOrdinal("document_id")),
                    Content = reader.GetString(reader.GetOrdinal("content")),
                    Score = reader.GetDouble(reader.GetOrdinal("score")),
                    Metadata = metadata
                });
            }

            ChatbotTelemetry.VectorStoreLatencyMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                new KeyValuePair<string, object?>("vector.operation", "search"),
                new KeyValuePair<string, object?>("vector.provider", "pgvector"));
            return results;
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            return await _fallbackGateway.HybridSearchAsync(query, queryEmbedding, topK, filters, ct);
        }
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
    {
        var dataSource = await GetDataSourceAsync(ct);
        if (dataSource is null)
        {
            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
            return;
        }

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM {GetQualifiedTableName()} WHERE document_id = @document_id;";
            command.Parameters.AddWithValue("document_id", NpgsqlDbType.Uuid, documentId);
            await command.ExecuteNonQueryAsync(ct);
            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
        }
    }

    private async Task<NpgsqlDataSource?> GetDataSourceAsync(CancellationToken ct)
    {
        if (!string.Equals(_options.Provider, "pgvector", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(_options.ConnectionString) || _pgVectorUnavailable)
        {
            return null;
        }

        if (_dataSource is not null)
        {
            return _dataSource;
        }

        await _initializationLock.WaitAsync(ct);
        try
        {
            if (_dataSource is not null)
            {
                return _dataSource;
            }

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(_options.ConnectionString);
            dataSourceBuilder.UseVector();
            _dataSource = dataSourceBuilder.Build();

            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = BuildInitializationSql();
            await command.ExecuteNonQueryAsync(ct);
            return _dataSource;
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            return null;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private void FillUpsertParameters(NpgsqlCommand command, DocumentChunkIndexDto chunk)
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

    private void FillSearchParameters(NpgsqlCommand command, string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters)
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

    private string BuildSearchSql(string query, float[]? queryEmbedding, FileSearchFilterDto? filters)
    {
        var hasQueryText = !string.IsNullOrWhiteSpace(query);
        var hasQueryEmbedding = queryEmbedding is { Length: > 0 };
        var scoreExpression = hasQueryText && hasQueryEmbedding
            ? "((dense_score * 0.65) + (lexical_score * 0.35))"
            : hasQueryEmbedding
            ? "dense_score"
            : "lexical_score";

        return $@"
WITH ranked AS (
    SELECT
        chunk_id,
        document_id,
        content,
        metadata,
        CASE
            WHEN @has_query_embedding THEN 1 - (embedding <=> @query_embedding)
            ELSE 0
        END AS dense_score,
        CASE
            WHEN length(trim(@query_text)) > 0 THEN ts_rank_cd(search_vector, plainto_tsquery('simple', @query_text))
            ELSE 0
        END AS lexical_score
    FROM {GetQualifiedTableName()}
    WHERE tenant_id = @tenant_id
      AND (@document_ids IS NULL OR document_id = ANY(@document_ids))
      AND (@tags IS NULL OR tags && @tags)
      AND (@categories IS NULL OR categories && @categories)
      AND (@content_types IS NULL OR content_type = ANY(@content_types))
      AND (@sources IS NULL OR source_type = ANY(@sources) OR source_name = ANY(@sources))
)
SELECT
    chunk_id,
    document_id,
    content,
    metadata,
    {scoreExpression} AS score
FROM ranked
ORDER BY score DESC, chunk_id ASC
LIMIT @top_k;";
    }

    private string BuildInitializationSql()
    {
        return $@"
CREATE EXTENSION IF NOT EXISTS vector;
CREATE SCHEMA IF NOT EXISTS {QuoteIdentifier(_options.Schema)};
CREATE TABLE IF NOT EXISTS {GetQualifiedTableName()} (
    tenant_id uuid NOT NULL,
    document_id uuid NOT NULL,
    chunk_id text NOT NULL,
    chunk_index integer NOT NULL,
    content text NOT NULL,
    normalized_text text NOT NULL,
    embedding vector({_options.Dimensions}) NOT NULL,
    metadata jsonb NOT NULL DEFAULT '{{}}'::jsonb,
    title text NOT NULL DEFAULT '',
    source_name text NOT NULL DEFAULT '',
    source_type text NOT NULL DEFAULT '',
    content_type text NOT NULL DEFAULT '',
    section_title text NOT NULL DEFAULT '',
    page_number integer NOT NULL DEFAULT 0,
    end_page_number integer NOT NULL DEFAULT 0,
    access_policy text NOT NULL DEFAULT '',
    tags text[] NOT NULL DEFAULT '{{}}',
    categories text[] NOT NULL DEFAULT '{{}}',
    content_hash text NOT NULL DEFAULT '',
    embedding_model_name text NOT NULL DEFAULT '',
    embedding_model_version text NOT NULL DEFAULT '',
    vector_dimensions integer NOT NULL DEFAULT {_options.Dimensions},
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    search_vector tsvector GENERATED ALWAYS AS (to_tsvector('simple', coalesce(normalized_text, ''))) STORED,
    PRIMARY KEY (tenant_id, chunk_id)
);
CREATE INDEX IF NOT EXISTS {QuoteIdentifier($"ix_{SanitizeIdentifier(_options.Schema)}_chunks_document")}
    ON {GetQualifiedTableName()} (tenant_id, document_id);
CREATE INDEX IF NOT EXISTS {QuoteIdentifier($"ix_{SanitizeIdentifier(_options.Schema)}_chunks_tags")}
    ON {GetQualifiedTableName()} USING gin (tags);
CREATE INDEX IF NOT EXISTS {QuoteIdentifier($"ix_{SanitizeIdentifier(_options.Schema)}_chunks_categories")}
    ON {GetQualifiedTableName()} USING gin (categories);
CREATE INDEX IF NOT EXISTS {QuoteIdentifier($"ix_{SanitizeIdentifier(_options.Schema)}_chunks_metadata")}
    ON {GetQualifiedTableName()} USING gin (metadata);
CREATE INDEX IF NOT EXISTS {QuoteIdentifier($"ix_{SanitizeIdentifier(_options.Schema)}_chunks_search_vector")}
    ON {GetQualifiedTableName()} USING gin (search_vector);
CREATE INDEX IF NOT EXISTS {QuoteIdentifier($"ix_{SanitizeIdentifier(_options.Schema)}_chunks_embedding_hnsw")}
    ON {GetQualifiedTableName()} USING hnsw (embedding vector_cosine_ops);";
    }

    private string GetQualifiedTableName()
    {
        return $"{QuoteIdentifier(_options.Schema)}.{QuoteIdentifier("document_chunks")}";
    }

    private void MarkUnavailable(Exception ex)
    {
        if (_pgVectorUnavailable)
        {
            return;
        }

        _pgVectorUnavailable = true;
        _logger.LogWarning(ex, "pgvector indisponivel; fallback local persistente sera usado.");
        _dataSource?.Dispose();
        _dataSource = null;
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

    private static string QuoteIdentifier(string value)
    {
        return $"\"{SanitizeIdentifier(value)}\"";
    }

    private static string SanitizeIdentifier(string value)
    {
        return string.Concat((value ?? string.Empty).Where(character => char.IsLetterOrDigit(character) || character == '_'));
    }
}

public sealed class SearchIndexBackedVectorStore : IVectorStore
{
    private readonly ISearchIndexGateway _searchIndexGateway;

    public SearchIndexBackedVectorStore(ISearchIndexGateway searchIndexGateway)
    {
        _searchIndexGateway = searchIndexGateway;
    }

    public async Task UpsertAsync(VectorUpsertRequest request, CancellationToken ct)
    {
        var chunks = request.Chunks.Select((chunk, index) => new DocumentChunkIndexDto
        {
            ChunkId = chunk.ChunkId,
            DocumentId = request.DocumentId,
            Content = chunk.Text,
            Embedding = chunk.Vector,
            PageNumber = ParseInt(ReadMetadata(chunk.Metadata, "startPage"), 0),
            Section = ReadMetadata(chunk.Metadata, "section"),
            Metadata = new Dictionary<string, string>(chunk.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["tenantId"] = request.TenantId.ToString(),
                ["chunkIndex"] = index.ToString(),
                ["vectorDimensions"] = chunk.Vector.Length.ToString()
            }
        }).ToList();

        await _searchIndexGateway.IndexDocumentChunksAsync(chunks, ct);
    }

    public async Task<VectorSearchResult> SearchAsync(VectorSearchRequest request, CancellationToken ct)
    {
        var filters = new FileSearchFilterDto
        {
            TenantId = request.TenantId,
            Categories = request.Filters.TryGetValue("categories", out var categories) ? categories.ToList() : null,
            Tags = request.Filters.TryGetValue("tags", out var tags) ? tags.ToList() : null,
            Sources = request.Filters.TryGetValue("sources", out var sources) ? sources.ToList() : null,
            ContentTypes = request.Filters.TryGetValue("contentTypes", out var contentTypes) ? contentTypes.ToList() : null,
            DocumentIds = request.Filters.TryGetValue("documentIds", out var documentIds)
                ? documentIds.Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty).Where(value => value != Guid.Empty).ToList()
                : null
        };

        var results = await _searchIndexGateway.HybridSearchAsync(request.QueryText, request.QueryVector, request.TopK, filters, ct);
        var filtered = results
            .Where(result => result.Score >= request.ScoreThreshold)
            .Select(result => new RetrievedChunk
            {
                ChunkId = result.ChunkId,
                DocumentId = result.DocumentId,
                Score = result.Score,
                Text = result.Content,
                Metadata = result.Metadata
            })
            .ToArray();

        return new VectorSearchResult
        {
            Chunks = filtered,
            Strategy = request.QueryVector is { Length: > 0 } ? "pgvector-hybrid" : "pgvector-lexical"
        };
    }

    public Task DeleteDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct)
    {
        return _searchIndexGateway.DeleteDocumentAsync(documentId, ct);
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