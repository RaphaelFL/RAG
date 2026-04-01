using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Ingestion.Parsers;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Observability;
using Chatbot.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Unit;

public class DocumentTextExtractorTests
{
    [Fact]
    public async Task ExtractAsync_ShouldPreferDirectParsing_ForTextDocuments()
    {
        var sut = CreateSut();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("Politica de viagens corporativas"));

        var result = await sut.ExtractAsync(new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "politica.txt",
            ContentType = "text/plain",
            Content = content
        }, CancellationToken.None);

        result.Strategy.Should().Be("direct");
        result.Text.Should().Be("Politica de viagens corporativas");
        result.Provider.Should().BeNull();
    }

    [Fact]
    public async Task ExtractAsync_ShouldFallbackToOcr_ForImageDocuments()
    {
        var sut = CreateSut();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("Imagem escaneada com texto"));

        var result = await sut.ExtractAsync(new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "scan.png",
            ContentType = "image/png",
            Content = content
        }, CancellationToken.None);

        result.Strategy.Should().Be("ocr");
        result.Provider.Should().Be("AzureDocumentIntelligence");
        result.Text.Should().Contain("Imagem escaneada");
    }

    [Fact]
    public async Task ExtractAsync_ShouldUseOcrFallbackProvider_WhenPrimaryReturnsNoText()
    {
        var sut = CreateSut();
        await using var content = new MemoryStream(Array.Empty<byte>());

        var result = await sut.ExtractAsync(new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "scan.png",
            ContentType = "image/png",
            Content = content
        }, CancellationToken.None);

        result.Strategy.Should().Be("ocr");
        result.Provider.Should().Be("GoogleVision");
        result.Text.Should().Contain("Fallback extracted text");
    }

    private static DocumentTextExtractor CreateSut()
    {
        var auditLogger = new SecurityAuditLogger(NullLogger<SecurityAuditLogger>.Instance);
        var ocrProvider = new MockOcrProvider(
            Options.Create(new OcrOptions
            {
                PrimaryProvider = "AzureDocumentIntelligence",
                FallbackProvider = "GoogleVision",
                EnableFallback = true
            }),
            auditLogger);

        return new DocumentTextExtractor(new[] { new DirectDocumentParser() }, ocrProvider);
    }
}