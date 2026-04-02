using System.Security.Cryptography;
using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Chatbot.Infrastructure.Configuration;

namespace Chatbot.Infrastructure.Providers;

public sealed class CachedEmbeddingProvider : IEmbeddingProvider
{
    private readonly IEmbeddingProvider _inner;
    private readonly IApplicationCache _cache;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly IRagRuntimeSettings _ragRuntimeSettings;

    public CachedEmbeddingProvider(
        IEmbeddingProvider inner,
        IApplicationCache cache,
        IRagRuntimeSettings ragRuntimeSettings,
        Microsoft.Extensions.Options.IOptions<EmbeddingOptions> embeddingOptions)
    {
        _inner = inner;
        _cache = cache;
        _embeddingOptions = embeddingOptions.Value;
        _ragRuntimeSettings = ragRuntimeSettings;
    }

    public async Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<float>();
        }

        var model = string.IsNullOrWhiteSpace(modelOverride)
            ? _embeddingOptions.Model
            : modelOverride.Trim();
        var key = $"embedding:{model}:{ComputeHash(Normalize(text))}";

        var cached = await _cache.GetAsync<float[]>(key, ct);
        if (cached is { Length: > 0 })
        {
            ChatbotTelemetry.EmbeddingReuse.Add(1, new KeyValuePair<string, object?>("reuse.kind", "cache-hit"));
            return cached;
        }

        var embedding = await _inner.CreateEmbeddingAsync(text, modelOverride, ct);
        if (embedding.Length > 0)
        {
            await _cache.SetAsync(key, embedding, _ragRuntimeSettings.EmbeddingCacheTtl, ct);
        }

        return embedding;
    }

    private static string Normalize(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}