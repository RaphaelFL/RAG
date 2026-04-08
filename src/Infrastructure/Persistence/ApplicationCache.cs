using System.Collections.Concurrent;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Chatbot.Infrastructure.Persistence;

public sealed class ApplicationCache : IApplicationCache, IDisposable
{
    private readonly ConcurrentDictionary<string, LocalCacheEntry> _memoryCache = new();
    private readonly FeatureFlagOptions _featureFlags;
    private readonly CacheOptions _cacheOptions;
    private readonly RedisSettings _redisSettings;
    private readonly ILogger<ApplicationCache> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _redisLock = new(1, 1);

    private ConnectionMultiplexer? _connection;
    private IDatabase? _database;
    private bool _redisUnavailable;

    public ApplicationCache(
        IOptions<FeatureFlagOptions> featureFlags,
        IOptions<CacheOptions> cacheOptions,
        IOptions<RedisSettings> redisSettings,
        ILogger<ApplicationCache> logger)
    {
        _featureFlags = featureFlags.Value;
        _cacheOptions = cacheOptions.Value;
        _redisSettings = redisSettings.Value;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        var normalizedKey = NormalizeKey(key);
        var cachedPayload = GetMemoryPayload(normalizedKey);
        if (cachedPayload is not null)
        {
            RecordCacheHit(key, "memory");
            return Deserialize<T>(cachedPayload);
        }

        var database = await GetDatabaseAsync(ct);
        if (database is null)
        {
            RecordCacheMiss(key);
            return default;
        }

        try
        {
            var redisValue = await database.StringGetAsync(normalizedKey);
            if (!redisValue.HasValue)
            {
                RecordCacheMiss(key);
                return default;
            }

            var payload = redisValue.ToString();
            SetMemoryPayload(normalizedKey, payload, TimeSpan.FromMinutes(5));
            RecordCacheHit(key, "redis");
            return Deserialize<T>(payload);
        }
        catch (Exception ex)
        {
            MarkRedisUnavailable(ex);
            RecordCacheMiss(key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
    {
        var normalizedKey = NormalizeKey(key);
        var payload = JsonSerializer.Serialize(value, _serializerOptions);
        SetMemoryPayload(normalizedKey, payload, ttl);

        var database = await GetDatabaseAsync(ct);
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
            MarkRedisUnavailable(ex);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct)
    {
        var normalizedKey = NormalizeKey(key);
        _memoryCache.TryRemove(normalizedKey, out _);

        var database = await GetDatabaseAsync(ct);
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
            MarkRedisUnavailable(ex);
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
        return _featureFlags.EnableRedisCache
            && !string.IsNullOrWhiteSpace(_redisSettings.Server)
            && _redisSettings.Port > 0;
    }

    private string? GetMemoryPayload(string key)
    {
        if (!_memoryCache.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _memoryCache.TryRemove(key, out _);
            return null;
        }

        return entry.Payload;
    }

    private void SetMemoryPayload(string key, string payload, TimeSpan ttl)
    {
        _memoryCache[key] = new LocalCacheEntry(payload, DateTime.UtcNow.Add(ttl));
        TrimMemoryCache();
    }

    private void TrimMemoryCache()
    {
        var limit = Math.Max(100, _cacheOptions.MaxInMemoryEntries);
        if (_memoryCache.Count <= limit)
        {
            return;
        }

        foreach (var expired in _memoryCache.Where(item => item.Value.ExpiresAtUtc <= DateTime.UtcNow).Select(item => item.Key).ToList())
        {
            _memoryCache.TryRemove(expired, out _);
        }

        while (_memoryCache.Count > limit)
        {
            var oldest = _memoryCache.OrderBy(item => item.Value.ExpiresAtUtc).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(oldest.Key))
            {
                break;
            }

            _memoryCache.TryRemove(oldest.Key, out _);
        }
    }

    private string NormalizeKey(string key)
    {
        return $"{_cacheOptions.InstancePrefix}:{key}";
    }

    private T? Deserialize<T>(string payload)
    {
        return JsonSerializer.Deserialize<T>(payload, _serializerOptions);
    }

    private void MarkRedisUnavailable(Exception ex)
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

    private static void RecordCacheHit(string key, string layer)
    {
        ChatbotTelemetry.CacheHits.Add(1, new KeyValuePair<string, object?>("cache.kind", ResolveKind(key)), new KeyValuePair<string, object?>("cache.layer", layer));
    }

    private static void RecordCacheMiss(string key)
    {
        ChatbotTelemetry.CacheMisses.Add(1, new KeyValuePair<string, object?>("cache.kind", ResolveKind(key)));
    }

    private static string ResolveKind(string key)
    {
        if (key.StartsWith("retrieval:", StringComparison.Ordinal))
        {
            return "retrieval";
        }

        if (key.StartsWith("chat-completion:", StringComparison.Ordinal))
        {
            return "chat-completion";
        }

        if (key.StartsWith("embedding:", StringComparison.Ordinal))
        {
            return "embedding";
        }

        return "generic";
    }
}