using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class FileSystemBlobPathResolver
{
    public FileSystemBlobPathResolver(LocalPersistenceOptions options, IHostEnvironment environment)
    {
        var resolvedBasePath = ResolveBasePath(options.BasePath, environment.ContentRootPath);
        RootPath = Path.Combine(resolvedBasePath, options.BlobRootDirectory);
    }

    public string RootPath { get; }

    public string Resolve(string relativePath)
    {
        var segments = relativePath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return Path.Combine(RootPath, Path.Combine(segments));
    }

    private static string ResolveBasePath(string configuredPath, string contentRootPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }
}