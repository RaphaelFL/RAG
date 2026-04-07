using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Unit.CurrentStateEmbeddingGenerationServiceTestsSupport;

internal sealed class CountingEmbeddingProvider : IEmbeddingProvider
{
    public int CallCount { get; private set; }

    public Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(new[] { 1f, 2f, 3f });
    }
}
