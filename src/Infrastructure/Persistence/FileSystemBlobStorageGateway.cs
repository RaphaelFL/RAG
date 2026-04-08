using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence;

public sealed class FileSystemBlobStorageGateway : IBlobStorageGateway
{
    private readonly FileSystemBlobPathResolver _pathResolver;
    private readonly BlobContentBuffer _contentBuffer;

    public FileSystemBlobStorageGateway(IOptions<LocalPersistenceOptions> options, IHostEnvironment environment)
        : this(new FileSystemBlobPathResolver(options.Value, environment), new BlobContentBuffer())
    {
    }

    internal FileSystemBlobStorageGateway(FileSystemBlobPathResolver pathResolver, BlobContentBuffer contentBuffer)
    {
        _pathResolver = pathResolver;
        _contentBuffer = contentBuffer;
        Directory.CreateDirectory(_pathResolver.RootPath);
    }

    public async Task<string> SaveAsync(Stream content, string path, CancellationToken ct)
    {
        var bytes = await _contentBuffer.ReadAsync(content, ct);
        var resolvedPath = _pathResolver.Resolve(path);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(resolvedPath, bytes, ct);

        return path;
    }

    public Task<Stream> GetAsync(string path, CancellationToken ct)
    {
        var resolvedPath = _pathResolver.Resolve(path);
        if (!File.Exists(resolvedPath))
        {
            throw new KeyNotFoundException($"Blob {path} not found");
        }

        Stream stream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        var resolvedPath = _pathResolver.Resolve(path);
        if (File.Exists(resolvedPath))
        {
            File.Delete(resolvedPath);
        }

        return Task.CompletedTask;
    }
}