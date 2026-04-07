using Chatbot.Application.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector.Npgsql;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class PgVectorDataSourceProvider
{
    private readonly VectorStoreOptions _options;
    private readonly ILogger<PgVectorSearchIndexGateway> _logger;
    private readonly PgVectorSqlBuilder _sqlBuilder;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    private NpgsqlDataSource? _dataSource;
    private bool _pgVectorUnavailable;

    public PgVectorDataSourceProvider(
        VectorStoreOptions options,
        ILogger<PgVectorSearchIndexGateway> logger,
        PgVectorSqlBuilder sqlBuilder)
    {
        _options = options;
        _logger = logger;
        _sqlBuilder = sqlBuilder;
    }

    public async Task<NpgsqlDataSource?> GetDataSourceAsync(CancellationToken ct)
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
            command.CommandText = _sqlBuilder.BuildInitializationSql();
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

    public void MarkUnavailable(Exception ex)
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
}