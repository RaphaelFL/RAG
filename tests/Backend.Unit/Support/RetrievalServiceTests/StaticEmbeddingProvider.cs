using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit.RetrievalServiceTestsSupport;

internal sealed class StaticEmbeddingProvider : IEmbeddingProvider
{
    private readonly float[] _embedding;

    public StaticEmbeddingProvider(float[]? embedding = null)
    {
        _embedding = embedding ?? new float[] { 0.25f, 0.5f, 0.75f };
    }

    public Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
    {
        return Task.FromResult(_embedding);
    }
}
