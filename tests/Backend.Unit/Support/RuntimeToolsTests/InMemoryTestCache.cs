using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Infrastructure.Tools;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Unit.RuntimeToolsTestsSupport;

internal sealed class InMemoryTestCache : IApplicationCache
{
    private readonly ConcurrentDictionary<string, object?> _items = new(StringComparer.OrdinalIgnoreCase);

    public Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        return Task.FromResult(_items.TryGetValue(key, out var value) ? (T?)value : default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
    {
        _items[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct)
    {
        _items.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
