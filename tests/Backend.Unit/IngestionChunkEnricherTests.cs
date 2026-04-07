using Chatbot.Application.Abstractions;
using Chatbot.Application.Services;
using FluentAssertions;
using Polly;
using Xunit;

using Backend.Unit.RagPersistenceTestsSupport;

namespace Backend.Unit;

public class IngestionChunkEnricherTests
{
    [Fact]
    public async Task EnrichAsync_ShouldReuseExistingEmbedding_WhenModelIsNotForced()
    {
        var provider = new CountingEmbeddingProvider();
        var sut = new IngestionChunkEnricher(provider, new ResiliencePipelineBuilder().Build());
        var chunks = new List<DocumentChunkIndexDto>
        {
            new()
            {
                ChunkId = "chunk-1",
                DocumentId = Guid.NewGuid(),
                Content = "Trecho indexado",
                Embedding = new[] { 0.1f, 0.2f },
                Metadata = new Dictionary<string, string>()
            }
        };

        var updated = await sut.EnrichAsync(chunks, null, false, CancellationToken.None);

        updated.Should().Be(0);
        provider.CallCount.Should().Be(0);
        chunks[0].Metadata.Should().ContainKey("contentHash");
    }

    [Fact]
    public async Task EnrichAsync_ShouldGenerateEmbeddingAndMetadata_WhenModelIsForced()
    {
        var provider = new CountingEmbeddingProvider();
        var sut = new IngestionChunkEnricher(provider, new ResiliencePipelineBuilder().Build());
        var chunks = new List<DocumentChunkIndexDto>
        {
            new()
            {
                ChunkId = "chunk-1",
                DocumentId = Guid.NewGuid(),
                Content = "Trecho sem embedding",
                Metadata = new Dictionary<string, string>()
            }
        };

        var updated = await sut.EnrichAsync(chunks, "text-embedding-3-large", false, CancellationToken.None);

        updated.Should().Be(1);
        provider.CallCount.Should().Be(1);
        chunks[0].Embedding.Should().NotBeNull();
        chunks[0].Metadata["embeddingModel"].Should().Be("text-embedding-3-large");
        chunks[0].Metadata.Should().ContainKey("contentHash");
    }
}