using System.Text;
using AppCfg = Chatbot.Application.Configuration;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisConnectionProvider : IRedisConnectionProvider, IDisposable
{
    private readonly AppCfg.VectorStoreOptions _vectorOptions;
    private readonly RedisSettings _redisSettings;
    private ConnectionMultiplexer? _connection;
    private IDatabase? _database;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RedisConnectionProvider(IOptions<AppCfg.VectorStoreOptions> vectorOptions, IOptions<RedisSettings> redisSettings)
    {
        _vectorOptions = vectorOptions.Value;
        _redisSettings = redisSettings.Value;
    }

    public async Task<IDatabase?> GetDatabaseAsync(CancellationToken ct)
    {
        if (_database is not null)
        {
            return _database;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_database is not null)
            {
                return _database;
            }

            var connectionString = ResolveConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return null;
            }

            _connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
            _database = _connection.GetDatabase();
            return _database;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _lock.Dispose();
    }

    private string ResolveConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(_vectorOptions.ConnectionString))
        {
            return _vectorOptions.ConnectionString;
        }

        if (string.IsNullOrWhiteSpace(_redisSettings.Server) || _redisSettings.Port <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append(_redisSettings.Server);
        builder.Append(':');
        builder.Append(_redisSettings.Port);
        builder.Append(",abortConnect=false");
        if (!string.IsNullOrWhiteSpace(_redisSettings.Password))
        {
            builder.Append(",password=");
            builder.Append(_redisSettings.Password);
        }

        return builder.ToString();
    }
}