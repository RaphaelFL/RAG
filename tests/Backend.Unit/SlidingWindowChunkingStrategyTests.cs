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

    [Fact]
    public void Chunk_ShouldIncreaseGranularity_ForLargerPdfSize_UsingSameRuntimeBaseline()
    {
        var sut = new SlidingWindowChunkingStrategy(new TestRagRuntimeSettings());
        var extraction = CreateNarrativeExtraction(4);

        var smallPdfChunks = sut.Chunk(CreateCommand(contentLength: 300_000), extraction);
        var largePdfChunks = sut.Chunk(CreateCommand(contentLength: 6_000_000), extraction);

        largePdfChunks.Count.Should().BeGreaterThan(smallPdfChunks.Count);
    }

    [Fact]
    public void Chunk_ShouldRespectAdminRuntimeChunkSize_AsBaseline()
    {
        var extraction = CreateNarrativeExtraction(4);
        var smallWindowChunks = new SlidingWindowChunkingStrategy(new ConfigurableRagRuntimeSettings(narrativeChunkSize: 160, narrativeOverlap: 48))
            .Chunk(CreateCommand(contentLength: 300_000), extraction);
        var largeWindowChunks = new SlidingWindowChunkingStrategy(new ConfigurableRagRuntimeSettings(narrativeChunkSize: 320, narrativeOverlap: 96))
            .Chunk(CreateCommand(contentLength: 300_000), extraction);

        smallWindowChunks.Count.Should().BeGreaterThan(largeWindowChunks.Count);
    }

    private static IngestDocumentCommand CreateCommand(long contentLength)
    {
        return new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FileName = "manual.pdf",
            ContentType = "application/pdf",
            DocumentTitle = "Manual Operacional",
            Source = "frontend-console",
            ContentLength = contentLength
        };
    }

    private static DocumentTextExtractionResultDto CreateNarrativeExtraction(int pageCount)
    {
        var pages = Enumerable.Range(1, pageCount)
            .Select(pageNumber => new PageExtractionDto
            {
                PageNumber = pageNumber,
                Text = string.Join(' ', new[]
                {
                    $"A pagina {pageNumber} detalha a validacao operacional antes do prosseguimento.",
                    "Cada etapa precisa manter contexto suficiente para auditoria e consulta posterior.",
                    "O fluxo registra criterios de aprovacao, excecoes e responsabilidades da equipe." 
                })
            })
            .ToList();

        return new DocumentTextExtractionResultDto
        {
            Text = string.Join("\n\n", pages.Select(page => page.Text)),
            Pages = pages
        };
    }

    private sealed class ConfigurableRagRuntimeSettings : IRagRuntimeSettings
    {
        public ConfigurableRagRuntimeSettings(int narrativeChunkSize, int narrativeOverlap)
        {
            NarrativeChunkSize = narrativeChunkSize;
            NarrativeOverlap = narrativeOverlap;
        }

        public int DenseChunkSize => 180;
        public int DenseOverlap => 72;
        public int NarrativeChunkSize { get; }
        public int NarrativeOverlap { get; }
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
