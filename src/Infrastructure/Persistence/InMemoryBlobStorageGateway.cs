using System.Collections.Concurrent;
using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

public sealed class InMemoryBlobStorageGateway : IBlobStorageGateway
{
    private static readonly ConcurrentDictionary<string, byte[]> Storage = new(StringComparer.OrdinalIgnoreCase);

    public Task<string> SaveAsync(Stream content, string path, CancellationToken ct)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var ms = new MemoryStream();
        content.CopyTo(ms);
        Storage[path] = ms.ToArray();

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        return Task.FromResult(path);
    }

    public Task<Stream> GetAsync(string path, CancellationToken ct)
    {
        if (!Storage.TryGetValue(path, out var data))
        {
            throw new KeyNotFoundException($"Blob {path} not found");
        }

        return Task.FromResult<Stream>(new MemoryStream(data));
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        Storage.TryRemove(path, out _);
        return Task.CompletedTask;
    }
}
