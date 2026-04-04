using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence;

public sealed class FileSystemBlobStorageGateway : IBlobStorageGateway
{
    private readonly string _blobRootPath;

    public FileSystemBlobStorageGateway(IOptions<LocalPersistenceOptions> options, IHostEnvironment environment)
    {
        var resolvedBasePath = ResolveBasePath(options.Value.BasePath, environment.ContentRootPath);
        _blobRootPath = Path.Combine(resolvedBasePath, options.Value.BlobRootDirectory);
        Directory.CreateDirectory(_blobRootPath);
    }

    public async Task<string> SaveAsync(Stream content, string path, CancellationToken ct)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var resolvedPath = ResolveBlobPath(path);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var target = new FileStream(resolvedPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(target, ct);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        return path;
    }

    public Task<Stream> GetAsync(string path, CancellationToken ct)
    {
        var resolvedPath = ResolveBlobPath(path);
        if (!File.Exists(resolvedPath))
        {
            throw new KeyNotFoundException($"Blob {path} not found");
        }

        Stream stream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        var resolvedPath = ResolveBlobPath(path);
        if (File.Exists(resolvedPath))
        {
            File.Delete(resolvedPath);
        }

        return Task.CompletedTask;
    }

    private string ResolveBlobPath(string relativePath)
    {
        var segments = relativePath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return Path.Combine(_blobRootPath, Path.Combine(segments));
    }

    private static string ResolveBasePath(string configuredPath, string contentRootPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }
}