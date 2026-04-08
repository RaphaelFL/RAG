using Chatbot.Application.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisStackDatabaseAccessor : IDisposable
{
    private readonly VectorStoreOptions _vectorOptions;
    private readonly ILogger _logger;
    private readonly IRedisConnectionProvider _connectionProvider;
    private readonly IRedisIndexEnsurer _indexEnsurer;
    private bool _redisSearchUnavailable;

    public RedisStackDatabaseAccessor(
        VectorStoreOptions vectorOptions,
        ILogger logger,
        IRedisConnectionProvider connectionProvider,
        IRedisIndexEnsurer indexEnsurer)
    {
        _vectorOptions = vectorOptions;
        _logger = logger;
        _connectionProvider = connectionProvider;
        _indexEnsurer = indexEnsurer;
    }

    public async Task<IDatabase?> GetDatabaseAsync(CancellationToken ct)
    {
        if (!string.Equals(_vectorOptions.Provider, "redisstack", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (_redisSearchUnavailable)
        {
            return null;
        }

        try
        {
            return await _connectionProvider.GetDatabaseAsync(ct);
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            return null;
        }
    }

    public async Task EnsureIndexAsync(IDatabase database, CancellationToken ct)
    {
        try
        {
            await _indexEnsurer.EnsureIndexAsync(database, ct);
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
        }
    }

    public void MarkUnavailable(Exception ex)
    {
        if (_redisSearchUnavailable)
        {
            return;
        }

        _redisSearchUnavailable = true;
        _logger.LogWarning(ex, "Redis Stack/RediSearch indisponivel; fallback local persistente sera usado.");
        if (_connectionProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public void Dispose()
    {
        if (_connectionProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}