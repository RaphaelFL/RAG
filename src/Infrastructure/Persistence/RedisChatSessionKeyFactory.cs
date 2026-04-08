using AppCfg = Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisChatSessionKeyFactory
{
    private readonly AppCfg.RedisCoordinationOptions _coordinationOptions;

    public RedisChatSessionKeyFactory(AppCfg.RedisCoordinationOptions coordinationOptions)
    {
        _coordinationOptions = coordinationOptions;
    }

    public string BuildSessionKey(Guid tenantId, Guid sessionId)
    {
        return $"{_coordinationOptions.KeyPrefix}:session:{tenantId:N}:{sessionId:N}";
    }

    public string BuildLockKey(Guid tenantId, Guid sessionId)
    {
        return $"{BuildSessionKey(tenantId, sessionId)}:lock";
    }
}