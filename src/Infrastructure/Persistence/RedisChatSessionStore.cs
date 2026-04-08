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
    private readonly ILogger<RedisChatSessionStore> _logger;
    private readonly RedisChatSessionDatabaseProvider _databaseProvider;
    private readonly RedisChatSessionLockManager _lockManager;
    private readonly RedisChatSessionKeyFactory _keyFactory;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public RedisChatSessionStore(
        InMemoryChatSessionStore fallbackStore,
        IOptions<AppCfg.RedisCoordinationOptions> coordinationOptions,
        IOptions<RedisSettings> redisSettings,
        ILogger<RedisChatSessionStore> logger)
    {
        _fallbackStore = fallbackStore;
        _logger = logger;
        _databaseProvider = new RedisChatSessionDatabaseProvider(coordinationOptions.Value, redisSettings.Value, logger);
        _lockManager = new RedisChatSessionLockManager(coordinationOptions.Value, logger);
        _keyFactory = new RedisChatSessionKeyFactory(coordinationOptions.Value);
    }

    public async Task AppendTurnAsync(ChatSessionTurnRecord record, CancellationToken ct)
    {
        await _fallbackStore.AppendTurnAsync(record, ct);

        var database = await _databaseProvider.GetDatabaseAsync(ct);
        if (database is null)
        {
            return;
        }

        var sessionKey = _keyFactory.BuildSessionKey(record.TenantId, record.SessionId);
        var lockKey = _keyFactory.BuildLockKey(record.TenantId, record.SessionId);
        var lockToken = Guid.NewGuid().ToString("N");

        try
        {
            var lockTaken = await _lockManager.TryAcquireAsync(database, lockKey, lockToken, ct);
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
                await _lockManager.SafeReleaseAsync(database, lockKey, lockToken);
            }
        }
        catch (Exception ex)
        {
            _databaseProvider.MarkUnavailable(ex);
        }
    }

    public async Task<ChatSessionSnapshot?> GetAsync(Guid sessionId, Guid tenantId, CancellationToken ct)
    {
        var database = await _databaseProvider.GetDatabaseAsync(ct);
        if (database is null)
        {
            return await _fallbackStore.GetAsync(sessionId, tenantId, ct);
        }

        try
        {
            var payload = await database.StringGetAsync(_keyFactory.BuildSessionKey(tenantId, sessionId));
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
            _databaseProvider.MarkUnavailable(ex);
            return await _fallbackStore.GetAsync(sessionId, tenantId, ct);
        }
    }

    public void Dispose()
    {
        _databaseProvider.Dispose();
    }

    private ChatSessionSnapshot? Deserialize(RedisValue payload)
    {
        return JsonSerializer.Deserialize<ChatSessionSnapshot>(payload.ToString(), _serializerOptions);
    }
}