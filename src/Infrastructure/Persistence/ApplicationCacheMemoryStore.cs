using System.Collections.Concurrent;
using Chatbot.Infrastructure.Configuration;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class ApplicationCacheMemoryStore
{
    private readonly ConcurrentDictionary<string, LocalCacheEntry> _entries = new();
    private readonly CacheOptions _cacheOptions;

    public ApplicationCacheMemoryStore(CacheOptions cacheOptions)
    {
        _cacheOptions = cacheOptions;
    }

    public string? GetPayload(string key)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _entries.TryRemove(key, out _);
            return null;
        }

        return entry.Payload;
    }

    public void SetPayload(string key, string payload, TimeSpan ttl)
    {
        _entries[key] = new LocalCacheEntry(payload, DateTime.UtcNow.Add(ttl));
        Trim();
    }

    public void Remove(string key)
    {
        _entries.TryRemove(key, out _);
    }

    private void Trim()
    {
        var limit = Math.Max(100, _cacheOptions.MaxInMemoryEntries);
        if (_entries.Count <= limit)
        {
            return;
        }

        foreach (var expired in _entries.Where(item => item.Value.ExpiresAtUtc <= DateTime.UtcNow).Select(item => item.Key).ToList())
        {
            _entries.TryRemove(expired, out _);
        }

        while (_entries.Count > limit)
        {
            var oldest = _entries.OrderBy(item => item.Value.ExpiresAtUtc).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(oldest.Key))
            {
                break;
            }

            _entries.TryRemove(oldest.Key, out _);
        }
    }
}