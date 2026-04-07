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

using Backend.Unit.DocumentTextExtractorTestsSupport;

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
    public async Task ExtractAsync_ShouldPreferDirectParsing_ForCsvDocuments()
    {
        var sut = CreateSut();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("nome,cargo\nMaria,Analista\nJoao,Coordenador"));

        var result = await sut.ExtractAsync(new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "colaboradores.csv",
            ContentType = "text/csv",
            Content = content
        }, CancellationToken.None);

        result.Strategy.Should().Be("direct");
        result.Text.Should().Contain("nome,cargo");
        result.Text.Should().Contain("Maria,Analista");
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
        var ocrOptions = Options.Create(new OcrOptions
        {
            PrimaryProvider = "AzureDocumentIntelligence",
            FallbackProvider = "GoogleVision",
            EnableFallback = true,
            EnableSelectiveOcr = true,
            MinimumDirectTextCharacters = 30,
            MinimumDirectTextCoverageRatio = 0.01
        });
        var sut = new DocumentTextExtractor(
            new[] { new DirectDocumentParser() },
            ocrProvider,
            new DocumentExtractionStrategyDecider(ocrOptions),
            new DocumentExtractionResultBuilder());

        const string pdfContent = "%PDF-1.4\n1 0 obj\n<< /Length 88 >>\nstream\nBT\n/F1 12 Tf\n72 720 Td\n(PDF textual com muito conteudo legivel para evitar OCR.) Tj\nET\nendstream\nendobj\n%%EOF";
        await using var content = new MemoryStream(Encoding.Latin1.GetBytes(pdfContent));

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
        var ocrOptions = Options.Create(new OcrOptions
        {
            PrimaryProvider = "AzureDocumentIntelligence",
            FallbackProvider = "GoogleVision",
            EnableFallback = true,
            EnableSelectiveOcr = true,
            MinimumDirectTextCharacters = 30,
            MinimumDirectTextCoverageRatio = 0.01
        });
        var sut = new DocumentTextExtractor(
            new[] { new DirectDocumentParser() },
            ocrProvider,
            new DocumentExtractionStrategyDecider(ocrOptions),
            new DocumentExtractionResultBuilder());

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
    public async Task ExtractAsync_ShouldFallbackToOcr_WhenPdfCannotBeParsedAndUtf8FallbackWouldBeGarbage()
    {
        var ocrProvider = new TrackingOcrProvider { Text = "texto OCR confiavel" };
        var ocrOptions = Options.Create(new OcrOptions
        {
            PrimaryProvider = "AzureDocumentIntelligence",
            FallbackProvider = "GoogleVision",
            EnableFallback = true,
            EnableSelectiveOcr = true,
            MinimumDirectTextCharacters = 30,
            MinimumDirectTextCoverageRatio = 0.01
        });
        var sut = new DocumentTextExtractor(
            new[] { new DirectDocumentParser() },
            ocrProvider,
            new DocumentExtractionStrategyDecider(ocrOptions),
            new DocumentExtractionResultBuilder());

        var pdfBytes = new byte[]
        {
            0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x0A,
            0x31, 0x20, 0x30, 0x20, 0x6F, 0x62, 0x6A, 0x0A,
            0x3C, 0x3C, 0x20, 0x2F, 0x4C, 0x65, 0x6E, 0x67, 0x74, 0x68, 0x20, 0x31, 0x32, 0x38, 0x20, 0x3E, 0x3E, 0x0A,
            0x73, 0x74, 0x72, 0x65, 0x61, 0x6D, 0x0A,
            0x78, 0x9C, 0x8B, 0x88, 0x20, 0x92, 0xA4, 0xE3, 0x10, 0x44, 0x6A, 0x19, 0xC2, 0xDF, 0xF0, 0x81,
            0xCA, 0xB7, 0x10, 0x99, 0xEE, 0x31, 0xD4, 0x8A, 0xB5, 0x7C, 0xE1, 0x00, 0x8A, 0x99, 0xFA, 0xCE,
            0x92, 0x11, 0xAF, 0x08, 0xC0, 0xD2, 0xFE, 0x99, 0x33, 0x54, 0xA0, 0x08, 0xD1, 0xA7, 0x5E, 0xB0,
            0x35, 0xA1, 0xE4, 0xF7, 0x3B, 0x9A, 0xCD, 0xA8, 0x91, 0x4C, 0x23, 0x04, 0x8E, 0xDD, 0x20, 0x17,
            0x65, 0x6E, 0x64, 0x73, 0x74, 0x72, 0x65, 0x61, 0x6D, 0x0A,
            0x65, 0x6E, 0x64, 0x6F, 0x62, 0x6A, 0x0A,
            0x25, 0x25, 0x45, 0x4F, 0x46
        };

        await using var content = new MemoryStream(pdfBytes);

        var result = await sut.ExtractAsync(new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "aso-demissional.pdf",
            ContentType = "application/pdf",
            ContentLength = content.Length,
            Content = content
        }, CancellationToken.None);

        result.Strategy.Should().Be("ocr");
        result.Text.Should().Be("texto OCR confiavel");
        ocrProvider.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ExtractAsync_ShouldFallbackToOcr_WhenPdfLiteralTextContainsControlCharacters()
    {
        var ocrProvider = new TrackingOcrProvider { Text = "ocr confiavel" };
        var ocrOptions = Options.Create(new OcrOptions
        {
            PrimaryProvider = "AzureDocumentIntelligence",
            FallbackProvider = "GoogleVision",
            EnableFallback = true,
            EnableSelectiveOcr = true,
            MinimumDirectTextCharacters = 30,
            MinimumDirectTextCoverageRatio = 0.01
        });
        var sut = new DocumentTextExtractor(
            new[] { new DirectDocumentParser() },
            ocrProvider,
            new DocumentExtractionStrategyDecider(ocrOptions),
            new DocumentExtractionResultBuilder());

        const string pdfContent = @"%PDF-1.4
1 0 obj
<< /Length 96 >>
stream
BT
/F1 12 Tf
72 720 Td
(\025\210\241\267\206.c\215T\343\260\251h\242\201\225&\323\254\346\220I-\264N\300\344\026PpjY\355-\356\020G<) Tj
ET
endstream
endobj
%%EOF";
        await using var content = new MemoryStream(Encoding.Latin1.GetBytes(pdfContent));

        var result = await sut.ExtractAsync(new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "aso-demissional.pdf",
            ContentType = "application/pdf",
            ContentLength = content.Length,
            Content = content
        }, CancellationToken.None);

        result.Strategy.Should().Be("ocr");
        result.Text.Should().Be("ocr confiavel");
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

        var ocrOptions = Options.Create(new OcrOptions
        {
            PrimaryProvider = "AzureDocumentIntelligence",
            FallbackProvider = "GoogleVision",
            EnableFallback = true,
            EnableSelectiveOcr = true,
            MinimumDirectTextCharacters = 120,
            MinimumDirectTextCoverageRatio = 0.02
        });

        return new DocumentTextExtractor(
            new[] { new DirectDocumentParser() },
            ocrProvider,
            new DocumentExtractionStrategyDecider(ocrOptions),
            new DocumentExtractionResultBuilder());
    }

}
