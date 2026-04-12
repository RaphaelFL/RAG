using System.Text;
using Chatbot.Api.Documents;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit;

public class DocumentUploadCommandFactoryTests
{
    [Fact]
    public void CreateUploadCommand_ShouldMapClientExtractedPayload_WhenProvided()
    {
        var sut = new DocumentUploadCommandFactory(NullLogger<DocumentUploadCommandFactory>.Instance);
        var payload = Encoding.UTF8.GetBytes("conteudo");
        using var content = new MemoryStream(payload);
        var file = new FormFile(content, 0, payload.Length, "file", "manual.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var command = sut.CreateUploadCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            file,
            content,
            new DocumentUploadFormData
            {
                ExtractedText = "Resumo executivo\n\nDetalhes da arquitetura",
                ExtractedPagesJson = "[{\"pageNumber\":1,\"text\":\"Resumo executivo\"},{\"pageNumber\":2,\"text\":\"Detalhes da arquitetura\"}]"
            });

        command.ClientExtractedText.Should().Be("Resumo executivo\n\nDetalhes da arquitetura");
        command.ClientExtractedPages.Should().HaveCount(2);
        command.ClientExtractedPages[0].PageNumber.Should().Be(1);
        command.ClientExtractedPages[1].Text.Should().Be("Detalhes da arquitetura");
    }
}