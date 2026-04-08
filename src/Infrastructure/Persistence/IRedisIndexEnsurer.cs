using StackExchange.Redis;

namespace Chatbot.Infrastructure.Persistence;

internal interface IRedisIndexEnsurer
{
    Task EnsureIndexAsync(IDatabase database, CancellationToken ct);
}