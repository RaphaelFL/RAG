using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit;

public class DocumentMetadataSuggestionServiceTests
{
    [Fact]
    public async Task SuggestAsync_ShouldInferArchitectureMetadata_FromExtractedText()
    {
        var sut = new DocumentMetadataSuggestionService(new StubDocumentTextExtractor(
            "ARQUITETURA DE INTEGRACAO CORPORATIVA\nEste documento descreve integracao entre APIs, servicos e sistema legado."),
            NullLogger<DocumentMetadataSuggestionService>.Instance);

        var result = await sut.SuggestAsync(new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "arquitetura-criado.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            Content = new MemoryStream(Encoding.UTF8.GetBytes("conteudo"))
        }, CancellationToken.None);

        result.SuggestedTitle.Should().Be("Arquitetura De Integracao Corporativa");
        result.SuggestedCategory.Should().Be("arquitetura");
        result.SuggestedCategories.Should().ContainSingle().Which.Should().Be("arquitetura");
        result.SuggestedTags.Should().Contain(tag => tag.Equals("arquitetura", StringComparison.OrdinalIgnoreCase));
        result.SuggestedTags.Should().Contain(tag => tag.Equals("integracao", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SuggestAsync_ShouldFallbackToFileName_WhenNoExtractedTextExists()
    {
        var sut = new DocumentMetadataSuggestionService(
            new StubDocumentTextExtractor(string.Empty),
            NullLogger<DocumentMetadataSuggestionService>.Instance);

        var result = await sut.SuggestAsync(new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "manual_reembolso_2026.txt",
            ContentType = "text/plain",
            Content = new MemoryStream(Encoding.UTF8.GetBytes("conteudo"))
        }, CancellationToken.None);

        result.SuggestedTitle.Should().Be("Manual Reembolso 2026");
        result.SuggestedTags.Should().Contain(tag => tag.Equals("reembolso", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SuggestAsync_ShouldFallbackToFileName_WhenPdfExtractionFails()
    {
        var sut = new DocumentMetadataSuggestionService(
            new ThrowingDocumentTextExtractor(new InvalidOperationException("ocr indisponivel")),
            NullLogger<DocumentMetadataSuggestionService>.Instance);

        var result = await sut.SuggestAsync(new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "aso_demissional_2026.pdf",
            ContentType = "application/pdf",
            Content = new MemoryStream(Array.Empty<byte>())
        }, CancellationToken.None);

        result.SuggestedTitle.Should().Be("Aso Demissional 2026");
        result.SuggestedCategory.Should().Be("rh");
        result.PreviewText.Should().BeEmpty();
        result.Strategy.Should().Be("heuristic-filename-fallback");
    }

    private sealed class StubDocumentTextExtractor : IDocumentTextExtractor
    {
        private readonly string _text;

        public StubDocumentTextExtractor(string text)
        {
            _text = text;
        }

        public Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct)
        {
            return Task.FromResult(new DocumentTextExtractionResultDto
            {
                Text = _text,
                Strategy = "direct"
            });
        }
    }

    private sealed class ThrowingDocumentTextExtractor : IDocumentTextExtractor
    {
        private readonly Exception _exception;

        public ThrowingDocumentTextExtractor(Exception exception)
        {
            _exception = exception;
        }

        public Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct)
        {
            throw _exception;
        }
    }
}