using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Providers;

public sealed class MockEmbeddingProvider : IEmbeddingProvider
{
    private readonly EmbeddingOptions _options;
    private const int LargeEmbeddingDimensions = 3072;
    private const int SmallEmbeddingDimensions = 1536;

    public MockEmbeddingProvider(IOptions<EmbeddingOptions> options)
    {
        _options = options.Value;
    }

    public Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(modelOverride) ? _options.Model : modelOverride;
        var dimensions = ResolveDimensions(model);
        var embedding = new float[dimensions];
        var random = new Random(HashCode.Combine(text, model));
        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)random.NextDouble();
        }

        return Task.FromResult(embedding);
    }

    private int ResolveDimensions(string? model)
    {
        if (string.Equals(model, "text-embedding-3-large", StringComparison.OrdinalIgnoreCase))
        {
            return LargeEmbeddingDimensions;
        }

        if (string.Equals(model, "text-embedding-3-small", StringComparison.OrdinalIgnoreCase))
        {
            return SmallEmbeddingDimensions;
        }

        return _options.Dimensions;
    }
}
