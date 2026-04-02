using Chatbot.Application.Abstractions;
using Chatbot.Ingestion.Chunking;
using FluentAssertions;
using Xunit;

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

    private sealed class TestRagRuntimeSettings : IRagRuntimeSettings
    {
        public int DenseChunkSize => 180;
        public int DenseOverlap => 72;
        public int NarrativeChunkSize => 180;
        public int NarrativeOverlap => 72;
        public int MinimumChunkCharacters => 40;
        public int RetrievalCandidateMultiplier => 3;
        public int RetrievalMaxCandidateCount => 24;
        public int MaxContextChunks => 4;
        public double MinimumRerankScore => 0.1;
        public double ExactMatchBoost => 0.18;
        public double TitleMatchBoost => 0.08;
        public double FilterMatchBoost => 0.05;
        public TimeSpan RetrievalCacheTtl => TimeSpan.FromMinutes(5);
        public TimeSpan ChatCompletionCacheTtl => TimeSpan.FromMinutes(10);
        public TimeSpan EmbeddingCacheTtl => TimeSpan.FromHours(24);
    }
}