using System.Diagnostics;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Chatbot.Infrastructure.Persistence;

public sealed class PgVectorSearchIndexGateway : ISearchIndexGateway
{
    private readonly ILogger<PgVectorSearchIndexGateway> _logger;
    private readonly ISearchIndexGateway _fallbackGateway;
    private readonly PgVectorSqlBuilder _sqlBuilder;
    private readonly PgVectorCommandParameterBinder _parameterBinder;
    private readonly PgVectorResultMapper _resultMapper;
    private readonly PgVectorDataSourceProvider _dataSourceProvider;

    public PgVectorSearchIndexGateway(
        IOptions<VectorStoreOptions> options,
        ILogger<PgVectorSearchIndexGateway> logger,
        ISearchIndexGateway fallbackGateway)
        : this(options.Value, logger, fallbackGateway)
    {
    }

    internal PgVectorSearchIndexGateway(
        VectorStoreOptions options,
        ILogger<PgVectorSearchIndexGateway> logger,
        ISearchIndexGateway fallbackGateway)
        : this(
            logger,
            fallbackGateway,
            new PgVectorSqlBuilder(options),
            new PgVectorCommandParameterBinder(options),
            new PgVectorResultMapper(),
            new PgVectorDataSourceProvider(options, logger, new PgVectorSqlBuilder(options)))
    {
    }

    internal PgVectorSearchIndexGateway(
        ILogger<PgVectorSearchIndexGateway> logger,
        ISearchIndexGateway fallbackGateway,
        PgVectorSqlBuilder sqlBuilder,
        PgVectorCommandParameterBinder parameterBinder,
        PgVectorResultMapper resultMapper,
        PgVectorDataSourceProvider dataSourceProvider)
    {
        _logger = logger;
        _fallbackGateway = fallbackGateway;
        _sqlBuilder = sqlBuilder;
        _parameterBinder = parameterBinder;
        _resultMapper = resultMapper;
        _dataSourceProvider = dataSourceProvider;
    }

    public async Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        var dataSource = await _dataSourceProvider.GetDataSourceAsync(ct);
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
                command.CommandText = _sqlBuilder.BuildUpsertSql();

                _parameterBinder.FillUpsertParameters(command, chunk);
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
            _dataSourceProvider.MarkUnavailable(ex);
            await _fallbackGateway.IndexDocumentChunksAsync(chunks, ct);
        }
    }

    public async Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct)
    {
        var dataSource = await _dataSourceProvider.GetDataSourceAsync(ct);
        if (dataSource is null)
        {
            return await _fallbackGateway.GetDocumentChunksAsync(documentId, ct);
        }

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = _sqlBuilder.BuildGetDocumentChunksSql();
            command.Parameters.AddWithValue("document_id", NpgsqlDbType.Uuid, documentId);

            var chunks = new List<DocumentChunkIndexDto>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                chunks.Add(_resultMapper.MapChunk(reader));
            }

            if (chunks.Count == 0)
            {
                return await _fallbackGateway.GetDocumentChunksAsync(documentId, ct);
            }

            return chunks;
        }
        catch (Exception ex)
        {
            _dataSourceProvider.MarkUnavailable(ex);
            return await _fallbackGateway.GetDocumentChunksAsync(documentId, ct);
        }
    }

    public async Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        var dataSource = await _dataSourceProvider.GetDataSourceAsync(ct);
        if (dataSource is null)
        {
            return await _fallbackGateway.HybridSearchAsync(query, queryEmbedding, topK, filters, ct);
        }

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = _sqlBuilder.BuildSearchSql(query, queryEmbedding is { Length: > 0 });
            _parameterBinder.FillSearchParameters(command, query, queryEmbedding, topK, filters);

            var results = new List<SearchResultDto>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(_resultMapper.MapSearchResult(reader));
            }

            ChatbotTelemetry.VectorStoreLatencyMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                new KeyValuePair<string, object?>("vector.operation", "search"),
                new KeyValuePair<string, object?>("vector.provider", "pgvector"));
            return results;
        }
        catch (Exception ex)
        {
            _dataSourceProvider.MarkUnavailable(ex);
            return await _fallbackGateway.HybridSearchAsync(query, queryEmbedding, topK, filters, ct);
        }
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
    {
        var dataSource = await _dataSourceProvider.GetDataSourceAsync(ct);
        if (dataSource is null)
        {
            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
            return;
        }

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = _sqlBuilder.BuildDeleteDocumentSql();
            command.Parameters.AddWithValue("document_id", NpgsqlDbType.Uuid, documentId);
            await command.ExecuteNonQueryAsync(ct);
            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
        }
        catch (Exception ex)
        {
            _dataSourceProvider.MarkUnavailable(ex);
            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
        }
    }
}