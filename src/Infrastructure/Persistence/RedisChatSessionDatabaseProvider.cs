using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using AppCfg = Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisChatSessionDatabaseProvider : IDisposable
{
    private readonly AppCfg.RedisCoordinationOptions _coordinationOptions;
    private readonly RedisSettings _redisSettings;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _redisLock = new(1, 1);

    private ConnectionMultiplexer? _connection;
    private IDatabase? _database;
    private bool _redisUnavailable;

    public RedisChatSessionDatabaseProvider(AppCfg.RedisCoordinationOptions coordinationOptions, RedisSettings redisSettings, ILogger logger)
    {
        _coordinationOptions = coordinationOptions;
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
        _logger.LogWarning(ex, "Redis de sessoes indisponivel; fallback local em memoria sera usado.");
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
        return _coordinationOptions.Enabled && BuildConfigurationOptionsOrNull() is not null;
    }

    private ConfigurationOptions BuildConfigurationOptions()
    {
        return BuildConfigurationOptionsOrNull()
            ?? throw new InvalidOperationException("Configuracao Redis indisponivel para persistencia de sessoes.");
    }

    private ConfigurationOptions? BuildConfigurationOptionsOrNull()
    {
        if (!string.IsNullOrWhiteSpace(_coordinationOptions.Configuration))
        {
            var parsed = ConfigurationOptions.Parse(_coordinationOptions.Configuration, true);
            parsed.AbortOnConnectFail = false;
            parsed.ConnectTimeout = 1500;
            parsed.SyncTimeout = 1500;
            return parsed;
        }

        if (string.IsNullOrWhiteSpace(_redisSettings.Server) || _redisSettings.Port <= 0)
        {
            return null;
        }

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