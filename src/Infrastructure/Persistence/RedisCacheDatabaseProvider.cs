using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisCacheDatabaseProvider : IDisposable
{
    private readonly FeatureFlagOptions _featureFlags;
    private readonly RedisSettings _redisSettings;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _redisLock = new(1, 1);

    private ConnectionMultiplexer? _connection;
    private IDatabase? _database;
    private bool _redisUnavailable;

    public RedisCacheDatabaseProvider(FeatureFlagOptions featureFlags, RedisSettings redisSettings, ILogger logger)
    {
        _featureFlags = featureFlags;
        _redisSettings = redisSettings;
        _logger = logger;
    }

    public async Task<IDatabase?> GetDatabaseAsync(CancellationToken ct)
    {
        if (!ShouldUseRedis() || _redisUnavailable)
        {
            return null;
        }

        if (_database is not null)
        {
            return _database;
        }

        await _redisLock.WaitAsync(ct);
        try
        {
            if (_database is not null)
            {
                return _database;
            }

            _connection = await ConnectionMultiplexer.ConnectAsync(BuildConfigurationOptions());
            _database = _connection.GetDatabase();
            return _database;
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            return null;
        }
        finally
        {
            _redisLock.Release();
        }
    }

    public void MarkUnavailable(Exception ex)
    {
        if (_redisUnavailable)
        {
            return;
        }

        _redisUnavailable = true;
        _logger.LogWarning(ex, "Redis cache indisponivel; fallback local em memoria sera usado.");
        _connection?.Dispose();
        _connection = null;
        _database = null;
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _redisLock.Dispose();
    }

    private bool ShouldUseRedis()
    {
        return _featureFlags.EnableRedisCache
            && !string.IsNullOrWhiteSpace(_redisSettings.Server)
            && _redisSettings.Port > 0;
    }

    private ConfigurationOptions BuildConfigurationOptions()
    {
        var configuration = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = 1500,
            SyncTimeout = 1500
        };
        configuration.EndPoints.Add(_redisSettings.Server, _redisSettings.Port);

        if (!string.IsNullOrWhiteSpace(_redisSettings.Password))
        {
            configuration.Password = _redisSettings.Password;
        }

        return configuration;
    }
}