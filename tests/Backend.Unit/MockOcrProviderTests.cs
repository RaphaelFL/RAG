using System.Text;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Observability;
using Chatbot.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Unit;

public class MockOcrProviderTests
{
    [Fact]
    public async Task ExtractAsync_ShouldUseFallbackText_WhenExtractionIsEmptyAndFallbackIsEnabled()
    {
        var provider = new MockOcrProvider(
            Options.Create(new OcrOptions
            {
                PrimaryProvider = "AzureDocumentIntelligence",
                FallbackProvider = "GoogleVision",
                EnableFallback = true
            }),
            new SecurityAuditLogger(NullLogger<SecurityAuditLogger>.Instance));

        await using var content = new MemoryStream(Array.Empty<byte>());

        var result = await provider.ExtractAsync(content, "scan.png", CancellationToken.None);

        result.Provider.Should().Be("GoogleVision");
        result.ExtractedText.Should().Contain("Fallback extracted text from scan.png");
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnOriginalText_WhenContentContainsText()
    {
        var provider = new MockOcrProvider(
            Options.Create(new OcrOptions()),
            new SecurityAuditLogger(NullLogger<SecurityAuditLogger>.Instance));

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("Politica de reembolso"));

        var result = await provider.ExtractAsync(content, "politica.txt", CancellationToken.None);

        result.Provider.Should().Be("AzureDocumentIntelligence");
        result.ExtractedText.Should().Be("Politica de reembolso");
    }

    [Fact]
    public async Task CreateEmbeddingAsync_ShouldHonorLargeModelOverride()
    {
        var provider = new MockEmbeddingProvider(Options.Create(new EmbeddingOptions
        {
            Model = "text-embedding-3-small",
            Dimensions = 1536
        }));

        var embedding = await provider.CreateEmbeddingAsync("conteudo", "text-embedding-3-large", CancellationToken.None);

        embedding.Should().HaveCount(3072);
    }
}