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

    [Fact]
    public async Task ExtractAsync_ShouldKeepDirectPdfText_WhenTextCoverageIsEnough()
    {
        var ocrProvider = new TrackingOcrProvider();
        var sut = new DocumentTextExtractor(
            new[] { new DirectDocumentParser() },
            ocrProvider,
            Options.Create(new OcrOptions
            {
                PrimaryProvider = "AzureDocumentIntelligence",
                FallbackProvider = "GoogleVision",
                EnableFallback = true,
                EnableSelectiveOcr = true,
                MinimumDirectTextCharacters = 30,
                MinimumDirectTextCoverageRatio = 0.01
            }));

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("PDF textual com muito conteudo legivel para evitar OCR."));

        var result = await sut.ExtractAsync(new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "manual.pdf",
            ContentType = "application/pdf",
            ContentLength = content.Length,
            Content = content
        }, CancellationToken.None);

        result.Strategy.Should().Be("direct");
        ocrProvider.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExtractAsync_ShouldExtractReadableText_FromPdfContentStream()
    {
        var sut = CreateSut();
        await using var content = new MemoryStream(Encoding.Latin1.GetBytes("%PDF-1.4\n1 0 obj\n<< /Length 55 >>\nstream\nBT\n/F1 12 Tf\n72 720 Td\n(Politica de reembolso corporativo) Tj\nET\nendstream\nendobj\n%%EOF"));

        var result = await sut.ExtractAsync(new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "manual.pdf",
            ContentType = "application/pdf",
            ContentLength = content.Length,
            Content = content
        }, CancellationToken.None);

        result.Strategy.Should().Be("direct");
        result.Text.Should().Contain("Politica de reembolso corporativo");
    }

    [Fact]
    public async Task ExtractAsync_ShouldFallbackToOcr_WhenPdfContainsOnlyBinaryArtifacts()
    {
        var ocrProvider = new TrackingOcrProvider { Text = "ocr extraido" };
        var sut = new DocumentTextExtractor(
            new[] { new DirectDocumentParser() },
            ocrProvider,
            Options.Create(new OcrOptions
            {
                PrimaryProvider = "AzureDocumentIntelligence",
                FallbackProvider = "GoogleVision",
                EnableFallback = true,
                EnableSelectiveOcr = true,
                MinimumDirectTextCharacters = 30,
                MinimumDirectTextCoverageRatio = 0.01
            }));

        await using var content = new MemoryStream(Encoding.Latin1.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\nxref\ntrailer\n%%EOF"));

        var result = await sut.ExtractAsync(new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "scan.pdf",
            ContentType = "application/pdf",
            ContentLength = content.Length,
            Content = content
        }, CancellationToken.None);

        result.Strategy.Should().Be("ocr");
        result.Text.Should().Be("ocr extraido");
        ocrProvider.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ExtractAsync_ShouldRemoveRepeatedHeadersFootersAndPageNumbers()
    {
        var sut = CreateSut();
        const string contentText = "RELATORIO FINANCEIRO\nPagina 1\n\n1. Introducao\n\nResumo executivo do periodo.\n\nConfidencial\fRELATORIO FINANCEIRO\nPagina 2\n\n2. Resultados\n\nDetalhes operacionais e financeiros.\n\nConfidencial";
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(contentText));

        var result = await sut.ExtractAsync(new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "relatorio.txt",
            ContentType = "text/plain",
            Content = content
        }, CancellationToken.None);

        result.Pages.Should().HaveCount(2);
        result.Text.Should().Contain("Resumo executivo do periodo.");
        result.Text.Should().Contain("Detalhes operacionais e financeiros.");
        result.Text.Should().NotContain("RELATORIO FINANCEIRO");
        result.Text.Should().NotContain("Pagina 1");
        result.Text.Should().NotContain("Confidencial");
    }

    private static DocumentTextExtractor CreateSut()
    {
        var auditLogger = new SecurityAuditLogger(NullLogger<SecurityAuditLogger>.Instance);
        var ocrProvider = new MockOcrProvider(
            Options.Create(new OcrOptions
            {
                PrimaryProvider = "AzureDocumentIntelligence",
                FallbackProvider = "GoogleVision",
                EnableFallback = true,
                EnableSelectiveOcr = true,
                MinimumDirectTextCharacters = 120,
                MinimumDirectTextCoverageRatio = 0.02
            }),
            auditLogger);

        return new DocumentTextExtractor(
            new[] { new DirectDocumentParser() },
            ocrProvider,
            Options.Create(new OcrOptions
            {
                PrimaryProvider = "AzureDocumentIntelligence",
                FallbackProvider = "GoogleVision",
                EnableFallback = true,
                EnableSelectiveOcr = true,
                MinimumDirectTextCharacters = 120,
                MinimumDirectTextCoverageRatio = 0.02
            }));
    }

    private sealed class TrackingOcrProvider : IOcrProvider
    {
        public int CallCount { get; private set; }
        public string Text { get; set; } = "ocr";

        public string ProviderName => "Tracking";

        public Task<OcrResultDto> ExtractAsync(Stream content, string fileName, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new OcrResultDto { ExtractedText = Text });
        }
    }
}