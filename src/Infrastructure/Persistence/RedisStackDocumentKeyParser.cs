using StackExchange.Redis;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisStackDocumentKeyParser
{
    public List<RedisKey> Parse(RedisResult response)
    {
        var keys = new List<RedisKey>();
        if (response.IsNull)
        {
            return keys;
        }

        var values = (RedisResult[])response!;
        for (var index = 1; index < values.Length; index++)
        {
            keys.Add(values[index].ToString());
        }

        return keys;
    }
}