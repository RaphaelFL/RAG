using Chatbot.Application.Observability;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class ApplicationCacheTelemetryRecorder
{
    public void RecordHit(string key, string layer)
    {
        ChatbotTelemetry.CacheHits.Add(1, new KeyValuePair<string, object?>("cache.kind", ApplicationCacheKeyResolver.ResolveKind(key)), new KeyValuePair<string, object?>("cache.layer", layer));
    }

    public void RecordMiss(string key)
    {
        ChatbotTelemetry.CacheMisses.Add(1, new KeyValuePair<string, object?>("cache.kind", ApplicationCacheKeyResolver.ResolveKind(key)));
    }
}