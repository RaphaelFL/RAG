using System.Diagnostics;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class PgVectorHybridSearchOperation
{
    private readonly ISearchIndexGateway _fallbackGateway;
    private readonly PgVectorSqlBuilder _sqlBuilder;
    private readonly PgVectorCommandParameterBinder _parameterBinder;
    private readonly PgVectorResultMapper _resultMapper;
    private readonly PgVectorDataSourceProvider _dataSourceProvider;

    public PgVectorHybridSearchOperation(
        ISearchIndexGateway fallbackGateway,
        PgVectorSqlBuilder sqlBuilder,
        PgVectorCommandParameterBinder parameterBinder,
        PgVectorResultMapper resultMapper,
        PgVectorDataSourceProvider dataSourceProvider)
    {
        _fallbackGateway = fallbackGateway;
        _sqlBuilder = sqlBuilder;
        _parameterBinder = parameterBinder;
        _resultMapper = resultMapper;
        _dataSourceProvider = dataSourceProvider;
    }

    public async Task<List<SearchResultDto>> ExecuteAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
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
}