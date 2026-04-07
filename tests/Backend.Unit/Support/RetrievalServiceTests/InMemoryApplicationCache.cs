using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit.RetrievalServiceTestsSupport;

internal sealed class InMemoryApplicationCache : IApplicationCache
{
    private readonly Dictionary<string, object> _entries = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        return Task.FromResult(_entries.TryGetValue(key, out var value) ? (T?)value : default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
    {
        _entries[key] = value!;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct)
    {
        _entries.Remove(key);
        return Task.CompletedTask;
    }
}
