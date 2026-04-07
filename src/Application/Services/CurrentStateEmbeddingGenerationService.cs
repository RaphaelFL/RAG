using System.Text;
using System.Text.Json;
using Chatbot.Application.Configuration;
using Chatbot.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Chatbot.Application.Services;

public sealed class CurrentStateEmbeddingGenerationService : IEmbeddingGenerationService
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly EmbeddingGenerationOptions _options;

    public CurrentStateEmbeddingGenerationService(
        IEmbeddingProvider embeddingProvider,
        IOptions<EmbeddingGenerationOptions> options)
    {
        _embeddingProvider = embeddingProvider;
        _options = options.Value;
    }

    public async Task<IReadOnlyCollection<EmbeddingEnvelope>> GenerateBatchAsync(EmbeddingBatchRequest request, CancellationToken ct)
    {
        var items = request.Inputs
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .GroupBy(item => item.ContentHash, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var generated = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            generated[item.ContentHash] = (await _embeddingProvider.CreateEmbeddingAsync(item.Text, null, ct)).ToArray();
        }

        return request.Inputs.Select(item => new EmbeddingEnvelope
        {
            ChunkId = item.ChunkId,
            EmbeddingModelName = string.IsNullOrWhiteSpace(request.EmbeddingModelName) ? _options.ModelName : request.EmbeddingModelName,
            EmbeddingModelVersion = string.IsNullOrWhiteSpace(request.EmbeddingModelVersion) ? _options.ModelVersion : request.EmbeddingModelVersion,
            VectorDimensions = generated.TryGetValue(item.ContentHash, out var vector) ? vector.Length : _options.Dimensions,
            Vector = generated.TryGetValue(item.ContentHash, out var resolvedVector) ? resolvedVector : Array.Empty<float>()
        }).ToArray();
    }
}
