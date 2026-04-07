using Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class PgVectorSqlBuilder
{
    private readonly VectorStoreOptions _options;

    public PgVectorSqlBuilder(VectorStoreOptions options)
    {
        _options = options;
    }

    public string BuildUpsertSql()
    {
        return $@"
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
    }

    public string BuildGetDocumentChunksSql()
    {
        return $@"
SELECT
    chunk_id,
    document_id,
    content,
    metadata,
    section_title,
    page_number,
    chunk_index,
    embedding::text AS embedding_text
FROM {GetQualifiedTableName()}
WHERE document_id = @document_id
ORDER BY chunk_index ASC, chunk_id ASC;";
    }

    public string BuildSearchSql(string query, bool hasQueryEmbedding)
    {
        var hasQueryText = !string.IsNullOrWhiteSpace(query);
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

    public string BuildDeleteDocumentSql()
    {
        return $"DELETE FROM {GetQualifiedTableName()} WHERE document_id = @document_id;";
    }

    public string BuildInitializationSql()
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

    private static string QuoteIdentifier(string value)
    {
        return $"\"{SanitizeIdentifier(value)}\"";
    }

    private static string SanitizeIdentifier(string value)
    {
        return string.Concat((value ?? string.Empty).Where(character => char.IsLetterOrDigit(character) || character == '_'));
    }
}