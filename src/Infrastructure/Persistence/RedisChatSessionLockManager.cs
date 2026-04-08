using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using AppCfg = Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisChatSessionLockManager
{
    private readonly AppCfg.RedisCoordinationOptions _coordinationOptions;
    private readonly ILogger _logger;

    public RedisChatSessionLockManager(AppCfg.RedisCoordinationOptions coordinationOptions, ILogger logger)
    {
        _coordinationOptions = coordinationOptions;
        _logger = logger;
    }

    public async Task<bool> TryAcquireAsync(IDatabase database, string lockKey, string lockToken, CancellationToken ct)
    {
        var expiry = TimeSpan.FromSeconds(Math.Max(5, _coordinationOptions.LockTimeoutSeconds));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (await database.LockTakeAsync(lockKey, lockToken, expiry))
            {
                return true;
            }

            await Task.Delay(50 * (attempt + 1), ct);
        }

        return false;
    }

    public async Task SafeReleaseAsync(IDatabase database, string lockKey, string lockToken)
    {
        try
        {
            await database.LockReleaseAsync(lockKey, lockToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao liberar lock Redis da sessao {lockKey}", lockKey);
        }
    }
}