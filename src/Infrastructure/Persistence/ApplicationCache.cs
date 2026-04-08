using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence;

public sealed class ApplicationCache : IApplicationCache, IDisposable
{
    private readonly ApplicationCacheKeyResolver _keyResolver;
    private readonly ApplicationCacheMemoryStore _memoryStore;
    private readonly ApplicationCacheTelemetryRecorder _telemetry;
    private readonly RedisCacheDatabaseProvider _databaseProvider;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public ApplicationCache(
        IOptions<FeatureFlagOptions> featureFlags,
        IOptions<CacheOptions> cacheOptions,
        IOptions<RedisSettings> redisSettings,
        ILogger<ApplicationCache> logger)
    {
        _keyResolver = new ApplicationCacheKeyResolver(cacheOptions.Value);
        _memoryStore = new ApplicationCacheMemoryStore(cacheOptions.Value);
        _telemetry = new ApplicationCacheTelemetryRecorder();
        _databaseProvider = new RedisCacheDatabaseProvider(featureFlags.Value, redisSettings.Value, logger);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        var normalizedKey = _keyResolver.NormalizeKey(key);
        var cachedPayload = _memoryStore.GetPayload(normalizedKey);
        if (cachedPayload is not null)
        {
            _telemetry.RecordHit(key, "memory");
            return Deserialize<T>(cachedPayload);
        }

        var database = await _databaseProvider.GetDatabaseAsync(ct);
        if (database is null)
        {
            _telemetry.RecordMiss(key);
            return default;
        }

        try
        {
            var redisValue = await database.StringGetAsync(normalizedKey);
            if (!redisValue.HasValue)
            {
                _telemetry.RecordMiss(key);
                return default;
            }

            var payload = redisValue.ToString();
            _memoryStore.SetPayload(normalizedKey, payload, TimeSpan.FromMinutes(5));
            _telemetry.RecordHit(key, "redis");
            return Deserialize<T>(payload);
        }
        catch (Exception ex)
        {
            _databaseProvider.MarkUnavailable(ex);
            _telemetry.RecordMiss(key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
    {
        var normalizedKey = _keyResolver.NormalizeKey(key);
        var payload = JsonSerializer.Serialize(value, _serializerOptions);
        _memoryStore.SetPayload(normalizedKey, payload, ttl);

        var database = await _databaseProvider.GetDatabaseAsync(ct);
        if (database is null)
        {
            return;
        }

        try
        {
            await database.StringSetAsync(normalizedKey, payload, ttl);
        }
        catch (Exception ex)
        {
            _databaseProvider.MarkUnavailable(ex);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct)
    {
        var normalizedKey = _keyResolver.NormalizeKey(key);
        _memoryStore.Remove(normalizedKey);

        var database = await _databaseProvider.GetDatabaseAsync(ct);
        if (database is null)
        {
            return;
        }

        try
        {
            await database.KeyDeleteAsync(normalizedKey);
        }
        catch (Exception ex)
        {
            _databaseProvider.MarkUnavailable(ex);
        }
    }

    public void Dispose()
    {
        _databaseProvider.Dispose();
    }

    private T? Deserialize<T>(string payload)
    {
        return JsonSerializer.Deserialize<T>(payload, _serializerOptions);
    }
}