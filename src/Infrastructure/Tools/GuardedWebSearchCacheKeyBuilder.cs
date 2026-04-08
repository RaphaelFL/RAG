using System.Security.Cryptography;
using System.Text;

namespace Chatbot.Infrastructure.Tools;

internal sealed class GuardedWebSearchCacheKeyBuilder
{
    public string Build(Guid tenantId, string query, int topK)
    {
        var payload = Encoding.UTF8.GetBytes($"{tenantId:N}:{topK}:{query.Trim()}");
        return $"web-search:{Convert.ToHexString(SHA256.HashData(payload))}";
    }
}