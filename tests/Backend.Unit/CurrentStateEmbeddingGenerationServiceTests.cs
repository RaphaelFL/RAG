using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Unit;

public class CurrentStateEmbeddingGenerationServiceTests
{
    [Fact]
    public async Task GenerateBatchAsync_ShouldReuseContentHashWithinBatch()
    {
        var provider = new CountingEmbeddingProvider();
        var service = new CurrentStateEmbeddingGenerationService(
            provider,
            Options.Create(new EmbeddingGenerationOptions
            {
                ModelName = "model",
                ModelVersion = "v1",
                Dimensions = 3,
                BatchSize = 10,
                PrimaryRuntime = "test"
            }));

        var result = await service.GenerateBatchAsync(new EmbeddingBatchRequest
        {
            Inputs = new[]
            {
                new EmbeddingInput { ChunkId = "1", ContentHash = "same", Text = "abc", TenantId = Guid.NewGuid() },
                new EmbeddingInput { ChunkId = "2", ContentHash = "same", Text = "abc", TenantId = Guid.NewGuid() }
            }
        }, CancellationToken.None);

        provider.CallCount.Should().Be(1);
        result.Should().HaveCount(2);
        result.All(item => item.VectorDimensions == 3).Should().BeTrue();
    }

    private sealed class CountingEmbeddingProvider : IEmbeddingProvider
    {
        public int CallCount { get; private set; }

        public Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new[] { 1f, 2f, 3f });
        }
    }
}