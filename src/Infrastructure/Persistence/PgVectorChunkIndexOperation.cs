using System.Diagnostics;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class PgVectorChunkIndexOperation
{
    private readonly ISearchIndexGateway _fallbackGateway;
    private readonly PgVectorSqlBuilder _sqlBuilder;
    private readonly PgVectorCommandParameterBinder _parameterBinder;
    private readonly PgVectorDataSourceProvider _dataSourceProvider;

    public PgVectorChunkIndexOperation(
        ISearchIndexGateway fallbackGateway,
        PgVectorSqlBuilder sqlBuilder,
        PgVectorCommandParameterBinder parameterBinder,
        PgVectorDataSourceProvider dataSourceProvider)
    {
        _fallbackGateway = fallbackGateway;
        _sqlBuilder = sqlBuilder;
        _parameterBinder = parameterBinder;
        _dataSourceProvider = dataSourceProvider;
    }

    public async Task ExecuteAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
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
}