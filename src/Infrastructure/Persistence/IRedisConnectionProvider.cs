using StackExchange.Redis;

namespace Chatbot.Infrastructure.Persistence;

internal interface IRedisConnectionProvider
{
    Task<IDatabase?> GetDatabaseAsync(CancellationToken ct);
}