using Chatbot.Application.Abstractions;
using Chatbot.Ingestion.Chunking;
using FluentAssertions;
using Xunit;

using Backend.Unit.SlidingWindowChunkingStrategyTestsSupport;

namespace Backend.Unit;

public class SlidingWindowChunkingStrategyTests
{
    [Fact]
    public void Chunk_ShouldPreserveSectionMetadata_PageRange_AndOverlap()
    {
        var sut = new SlidingWindowChunkingStrategy(new TestRagRuntimeSettings());
        var command = new IngestDocumentCommand
        {
            DocumentId = Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb"),
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FileName = "manual.txt",
            ContentType = "text/plain",
            DocumentTitle = "Manual Operacional",
            Source = "frontend-console"
        };

        var extraction = new DocumentTextExtractionResultDto
        {
            Text = string.Join("\n\n", new[]
            {
                "1. Introducao",
                "Este paragrafo apresenta o contexto inicial do processo e descreve os objetivos operacionais com detalhes suficientes para ocupar espaco.",
                "Este paragrafo de transicao precisa aparecer no proximo chunk para manter o contexto entre secoes consecutivas.",
                "2. Procedimento",
                "Este paragrafo explica as etapas seguintes e continua a narrativa com informacoes complementares importantes para a busca."
            }),
            Pages = new List<PageExtractionDto>
            {
                new()
                {
                    PageNumber = 1,
                    Text = "1. Introducao\n\nEste paragrafo apresenta o contexto inicial do processo e descreve os objetivos operacionais com detalhes suficientes para ocupar espaco.\n\nEste paragrafo de transicao precisa aparecer no proximo chunk para manter o contexto entre secoes consecutivas."
                },
                new()
                {
                    PageNumber = 2,
                    Text = "2. Procedimento\n\nEste paragrafo explica as etapas seguintes e continua a narrativa com informacoes complementares importantes para a busca."
                }
            }
        };

        var chunks = sut.Chunk(command, extraction);

        chunks.Should().HaveCountGreaterThanOrEqualTo(2);
        chunks[0].Section.Should().Be("Introducao");
        chunks[0].Metadata["startPage"].Should().Be("1");
        chunks[^1].Metadata["endPage"].Should().Be("2");
        chunks[^1].Content.Should().Contain("Este paragrafo de transicao precisa aparecer no proximo chunk");
        chunks.Should().OnlyContain(chunk => chunk.Metadata.ContainsKey("estimatedTokens"));
    }

}
