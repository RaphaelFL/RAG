using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using AppCfg = Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure.Persistence;

public sealed class RedisChatSessionStore : IChatSessionStore, IDisposable
{
    private readonly InMemoryChatSessionStore _fallbackStore;
    private readonly AppCfg.RedisCoordinationOptions _coordinationOptions;
    private readonly RedisSettings _redisSettings;
    private readonly ILogger<RedisChatSessionStore> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _redisLock = new(1, 1);

    private ConnectionMultiplexer? _connection;
    private IDatabase? _database;
    private bool _redisUnavailable;

    public RedisChatSessionStore(
        InMemoryChatSessionStore fallbackStore,
        IOptions<AppCfg.RedisCoordinationOptions> coordinationOptions,
        IOptions<RedisSettings> redisSettings,
        ILogger<RedisChatSessionStore> logger)
    {
        _fallbackStore = fallbackStore;
        _coordinationOptions = coordinationOptions.Value;
        _redisSettings = redisSettings.Value;
        _logger = logger;
    }

    public async Task AppendTurnAsync(ChatSessionTurnRecord record, CancellationToken ct)
    {
        await _fallbackStore.AppendTurnAsync(record, ct);

        var database = await GetDatabaseAsync(ct);
        if (database is null)
        {
            return;
        }

        var sessionKey = BuildSessionKey(record.TenantId, record.SessionId);
        var lockKey = BuildLockKey(record.TenantId, record.SessionId);
        var lockToken = Guid.NewGuid().ToString("N");

        try
        {
            var lockTaken = await TryAcquireLockAsync(database, lockKey, lockToken);
            if (!lockTaken)
            {
                _logger.LogWarning("Nao foi possivel adquirir lock Redis para a sessao {sessionId}; fallback local sera mantido.", record.SessionId);
                return;
            }

            try
            {
                var existingPayload = await database.StringGetAsync(sessionKey);
                var snapshot = existingPayload.HasValue
                    ? Deserialize(existingPayload!)
                    : null;

                var updatedSnapshot = snapshot is null
                    ? ChatSessionSnapshotFactory.Create(record)
                    : ChatSessionSnapshotFactory.Append(snapshot, record);

                var payload = JsonSerializer.Serialize(updatedSnapshot, _serializerOptions);
                await database.StringSetAsync(sessionKey, payload);
            }
            finally
            {
                await SafeReleaseLockAsync(database, lockKey, lockToken);
            }
        }
        catch (Exception ex)
        {
            MarkRedisUnavailable(ex);
        }
    }

    public async Task<ChatSessionSnapshot?> GetAsync(Guid sessionId, Guid tenantId, CancellationToken ct)
    {
        var database = await GetDatabaseAsync(ct);
        if (database is null)
        {
            return await _fallbackStore.GetAsync(sessionId, tenantId, ct);
        }

        try
        {
            var payload = await database.StringGetAsync(BuildSessionKey(tenantId, sessionId));
            if (!payload.HasValue)
            {
                return await _fallbackStore.GetAsync(sessionId, tenantId, ct);
            }

            var snapshot = Deserialize(payload!);
            if (snapshot is null || snapshot.TenantId != tenantId)
            {
                return null;
            }

            return ChatSessionSnapshotFactory.Clone(snapshot);
        }
        catch (Exception ex)
        {
            MarkRedisUnavailable(ex);
            return await _fallbackStore.GetAsync(sessionId, tenantId, ct);
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _redisLock.Dispose();
    }

    private async Task<IDatabase?> GetDatabaseAsync(CancellationToken ct)
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

            var configuration = BuildConfigurationOptions();
            _connection = await ConnectionMultiplexer.ConnectAsync(configuration);
            _database = _connection.GetDatabase();
            return _database;
        }
        catch (Exception ex)
        {
            MarkRedisUnavailable(ex);
            return null;
        }
        finally
        {
            _redisLock.Release();
        }
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

    private async Task<bool> TryAcquireLockAsync(IDatabase database, string lockKey, string lockToken)
    {
        var expiry = TimeSpan.FromSeconds(Math.Max(5, _coordinationOptions.LockTimeoutSeconds));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (await database.LockTakeAsync(lockKey, lockToken, expiry))
            {
                return true;
            }

            await Task.Delay(50 * (attempt + 1));
        }

        return false;
    }

    private async Task SafeReleaseLockAsync(IDatabase database, string lockKey, string lockToken)
    {
        try
        {
            await database.LockReleaseAsync(lockKey, lockToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao liberar lock Redis da sessao {lockKey}", lockKey);
        }
    }

    private string BuildSessionKey(Guid tenantId, Guid sessionId)
    {
        return $"{_coordinationOptions.KeyPrefix}:session:{tenantId:N}:{sessionId:N}";
    }

    private string BuildLockKey(Guid tenantId, Guid sessionId)
    {
        return $"{BuildSessionKey(tenantId, sessionId)}:lock";
    }

    private ChatSessionSnapshot? Deserialize(RedisValue payload)
    {
        return JsonSerializer.Deserialize<ChatSessionSnapshot>(payload.ToString(), _serializerOptions);
    }

    private void MarkRedisUnavailable(Exception ex)
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
}