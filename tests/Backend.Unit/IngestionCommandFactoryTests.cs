using Chatbot.Application.Abstractions;
using Chatbot.Application.Services;
using FluentAssertions;
using Xunit;

namespace Backend.Unit;

public class IngestionCommandFactoryTests
{
    [Fact]
    public void Create_ShouldHydrateClientExtraction_FromDocumentCatalogEntry()
    {
        var sut = new IngestionCommandFactory();

        var command = sut.Create(new DocumentCatalogEntry
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Title = "Manual tecnico",
            OriginalFileName = "manual.pdf",
            ContentType = "application/pdf",
            ClientExtractedText = "Resumo executivo\n\nDetalhes",
            ClientExtractedPages = new List<PageExtractionDto>
            {
                new() { PageNumber = 1, Text = "Resumo executivo" },
                new() { PageNumber = 2, Text = "Detalhes" }
            }
        }, new byte[] { 1, 2, 3 });

        command.ClientExtractedText.Should().Be("Resumo executivo\n\nDetalhes");
        command.ClientExtractedPages.Should().HaveCount(2);
        command.ClientExtractedPages[0].PageNumber.Should().Be(1);
        command.ClientExtractedPages[1].Text.Should().Be("Detalhes");
    }
}