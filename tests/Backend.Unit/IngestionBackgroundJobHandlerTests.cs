using Chatbot.Application.Abstractions;
using Chatbot.Application.Services;
using Chatbot.Domain.Entities;
using Chatbot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Xunit;

namespace Backend.Unit;

public class IngestionBackgroundJobHandlerTests
{
    [Fact]
    public async Task ProcessAsync_ShouldFail_WhenExtractionContainsOnlyFallbackPlaceholder()
    {
        var documentId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var catalog = new InMemoryDocumentCatalog();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = tenantId,
            Title = "Trabalho Final",
            OriginalFileName = "Trabalho_Final.pdf",
            ContentType = "application/pdf",
            Status = DocumentStatuses.Queued,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IndexedChunkCount = 3,
            Chunks = new List<DocumentChunkIndexDto>
            {
                new()
                {
                    ChunkId = "stale-chunk",
                    DocumentId = documentId,
                    Content = "stale",
                    Metadata = new Dictionary<string, string>()
                }
            }
        });

        var chunkingStrategy = new TrackingChunkingStrategy();
        var chunkEnricher = new TrackingChunkEnricher();
        var searchIndexGateway = new TrackingSearchIndexGateway();
        var resiliencePipeline = new ResiliencePipelineBuilder().Build();
        var sut = new IngestionBackgroundJobHandler(
            new IngestionCommandFactory(),
            new StaticIngestionExtractionService(new DocumentTextExtractionResultDto
            {
                Text = "Conteudo indisponivel para Trabalho_Final.pdf",
                Strategy = "ocr",
                Provider = "MockFallback",
                Pages = new List<PageExtractionDto>
                {
                    new()
                    {
                        PageNumber = 1,
                        Text = "Conteudo indisponivel para Trabalho_Final.pdf"
                    }
                }
            }),
            chunkEnricher,
            new IngestionDocumentStateService(catalog),
            searchIndexGateway,
            chunkingStrategy,
            resiliencePipeline,
            NullLogger<IngestionBackgroundJobHandler>.Instance);

        await sut.ProcessAsync(new IngestionBackgroundJob
        {
            JobId = jobId,
            DocumentId = documentId,
            TenantId = tenantId,
            FileName = "Trabalho_Final.pdf",
            ContentType = "application/pdf",
            Payload = Array.Empty<byte>(),
            RawHash = "hash",
            StoragePath = "documents/test/raw-content"
        }, CancellationToken.None);

        var stored = catalog.Get(documentId);

        stored.Should().NotBeNull();
        stored!.Status.Should().Be(DocumentStatuses.Failed);
        stored.LastJobId.Should().Be(jobId);
        stored.IndexedChunkCount.Should().Be(0);
        stored.Chunks.Should().BeEmpty();
        chunkingStrategy.CallCount.Should().Be(0);
        chunkEnricher.CallCount.Should().Be(0);
        searchIndexGateway.DeleteCallCount.Should().Be(0);
        searchIndexGateway.IndexCallCount.Should().Be(0);
    }

    private sealed class StaticIngestionExtractionService : IIngestionExtractionService
    {
        private readonly DocumentTextExtractionResultDto _result;

        public StaticIngestionExtractionService(DocumentTextExtractionResultDto result)
        {
            _result = result;
        }

        public Task<DocumentTextExtractionResultDto> ExtractAsync(Guid documentId, IngestDocumentCommand command, CancellationToken ct)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class TrackingChunkingStrategy : IChunkingStrategy
    {
        public int CallCount { get; private set; }

        public List<DocumentChunkIndexDto> Chunk(IngestDocumentCommand command, DocumentTextExtractionResultDto extraction)
        {
            CallCount += 1;
            return new List<DocumentChunkIndexDto>();
        }
    }

    private sealed class TrackingChunkEnricher : IIngestionChunkEnricher
    {
        public int CallCount { get; private set; }

        public Task<int> EnrichAsync(List<DocumentChunkIndexDto> chunks, string? forceEmbeddingModel, bool forceRefresh, CancellationToken ct)
        {
            CallCount += 1;
            return Task.FromResult(chunks.Count);
        }
    }

    private sealed class TrackingSearchIndexGateway : ISearchIndexGateway
    {
        public int DeleteCallCount { get; private set; }
        public int IndexCallCount { get; private set; }

        public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
        {
            DeleteCallCount += 1;
            return Task.CompletedTask;
        }

        public Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct)
        {
            return Task.FromResult(new List<DocumentChunkIndexDto>());
        }

        public Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
        {
            return Task.FromResult(new List<SearchResultDto>());
        }

        public Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
        {
            IndexCallCount += 1;
            return Task.CompletedTask;
        }
    }
}