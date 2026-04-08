using Chatbot.Infrastructure.Configuration;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class ApplicationCacheKeyResolver
{
    private readonly CacheOptions _cacheOptions;

    public ApplicationCacheKeyResolver(CacheOptions cacheOptions)
    {
        _cacheOptions = cacheOptions;
    }

    public string NormalizeKey(string key)
    {
        return $"{_cacheOptions.InstancePrefix}:{key}";
    }

    public static string ResolveKind(string key)
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