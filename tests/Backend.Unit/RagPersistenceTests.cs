using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Xunit;

namespace Backend.Unit;

public class RagPersistenceTests
{
    [Fact]
    public void FileSystemDocumentCatalog_ShouldPersistOnlyDocumentMetadataAndChunkCount()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "rag-persistence-tests", Guid.NewGuid().ToString("N"));
        var options = Options.Create(new LocalPersistenceOptions { BasePath = tempPath });
        var environment = new TestHostEnvironment();
        var catalog = new FileSystemDocumentCatalog(options, environment);
        var documentId = Guid.NewGuid();

        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = Guid.NewGuid(),
            Title = "Manual persistido",
            Status = "Indexed",
            IndexedChunkCount = 0,
            Chunks = new List<DocumentChunkIndexDto>
            {
                new()
                {
                    ChunkId = "chunk-001",
                    DocumentId = documentId,
                    Content = "Conteudo do chunk",
                    Embedding = new[] { 0.1f, 0.2f, 0.3f },
                    PageNumber = 1,
                    Metadata = new Dictionary<string, string> { ["chunkIndex"] = "0" }
                }
            }
        });

        var catalogFile = Path.Combine(tempPath, "document-catalog.json");
        var rawJson = File.ReadAllText(catalogFile);

        rawJson.Should().Contain("\"indexedChunkCount\": 1");
        rawJson.Should().Contain("\"chunks\": []");

        var reloadedCatalog = new FileSystemDocumentCatalog(options, environment);
        var stored = reloadedCatalog.Get(documentId);

        stored.Should().NotBeNull();
        stored!.IndexedChunkCount.Should().Be(1);
        stored.Chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task LocalPersistentSearchIndexGateway_ShouldRecoverPersistedChunksByDocumentId()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "rag-index-tests", Guid.NewGuid().ToString("N"));
        var options = Options.Create(new LocalPersistenceOptions { BasePath = tempPath });
        var environment = new TestHostEnvironment();
        var catalog = new InMemoryDocumentCatalog();
        var documentId = Guid.NewGuid();
        var chunk = new DocumentChunkIndexDto
        {
            ChunkId = "chunk-002",
            DocumentId = documentId,
            Content = "Trecho persistido no indice",
            Embedding = new[] { 0.4f, 0.5f, 0.6f },
            PageNumber = 2,
            Section = "Financeiro",
            Metadata = new Dictionary<string, string>
            {
                ["chunkIndex"] = "1",
                ["startPage"] = "2",
                ["section"] = "Financeiro"
            }
        };

        var writer = new LocalPersistentSearchIndexGateway(catalog, options, environment);
        await writer.IndexDocumentChunksAsync(new List<DocumentChunkIndexDto> { chunk }, CancellationToken.None);

        var reader = new LocalPersistentSearchIndexGateway(catalog, options, environment);
        var restored = await reader.GetDocumentChunksAsync(documentId, CancellationToken.None);

        restored.Should().ContainSingle();
        restored[0].ChunkId.Should().Be("chunk-002");
        restored[0].Content.Should().Be("Trecho persistido no indice");
        restored[0].PageNumber.Should().Be(2);
        restored[0].Section.Should().Be("Financeiro");
        restored[0].Embedding.Should().Equal(0.4f, 0.5f, 0.6f);
    }

    [Fact]
    public async Task ProcessReindexAsync_ShouldReusePersistedChunksFromIndex_WhenCatalogDoesNotStoreChunkPayload()
    {
        var documentId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var catalog = new InMemoryDocumentCatalog();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = Guid.NewGuid(),
            Title = "Documento indexado",
            Status = "Indexed",
            Version = 3,
            StoragePath = "documents/test/doc/raw-content",
            IndexedChunkCount = 1,
            Chunks = new List<DocumentChunkIndexDto>()
        });

        var searchIndexGateway = new CapturingPersistentChunkGateway(documentId);
        var embeddingProvider = new CountingEmbeddingProvider();
        var sut = new IngestionJobProcessor(
            new NoOpDocumentTextExtractor(),
            embeddingProvider,
            searchIndexGateway,
            new NoOpChunkingStrategy(),
            catalog,
            new NoOpBlobStorageGateway(),
            new NoOpPromptInjectionDetector(),
            new NoOpSecurityAuditLogger(),
            new ResiliencePipelineBuilder().Build(),
            NullLogger<IngestionJobProcessor>.Instance);

        await sut.ProcessReindexAsync(new ReindexBackgroundJob
        {
            JobId = jobId,
            DocumentId = documentId,
            FullReindex = false,
            ForceEmbeddingModel = null
        }, CancellationToken.None);

        embeddingProvider.CallCount.Should().Be(0);
        searchIndexGateway.ReindexedChunks.Should().BeEmpty();

        var updated = catalog.Get(documentId);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("Indexed");
        updated.IndexedChunkCount.Should().Be(1);
        updated.Chunks.Should().BeEmpty();
        updated.LastJobId.Should().Be(jobId);
        updated.Version.Should().Be(4);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Backend.Unit";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class CapturingPersistentChunkGateway : ISearchIndexGateway
    {
        private readonly Guid _documentId;

        public CapturingPersistentChunkGateway(Guid documentId)
        {
            _documentId = documentId;
        }

        public List<DocumentChunkIndexDto> ReindexedChunks { get; } = new();

        public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct)
        {
            if (documentId != _documentId)
            {
                return Task.FromResult(new List<DocumentChunkIndexDto>());
            }

            return Task.FromResult(new List<DocumentChunkIndexDto>
            {
                new()
                {
                    ChunkId = "chunk-003",
                    DocumentId = _documentId,
                    Content = "Chunk recuperado do indice persistido",
                    Embedding = new[] { 0.7f, 0.8f, 0.9f },
                    PageNumber = 3,
                    Section = "Operacao",
                    Metadata = new Dictionary<string, string>
                    {
                        ["chunkIndex"] = "0",
                        ["contentHash"] = "hash-atual",
                        ["embeddingModel"] = "default"
                    }
                }
            });
        }

        public Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
        {
            return Task.FromResult(new List<SearchResultDto>());
        }

        public Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
        {
            ReindexedChunks.AddRange(chunks.Select(chunk => new DocumentChunkIndexDto
            {
                ChunkId = chunk.ChunkId,
                DocumentId = chunk.DocumentId,
                Content = chunk.Content,
                Embedding = chunk.Embedding?.ToArray(),
                PageNumber = chunk.PageNumber,
                Section = chunk.Section,
                Metadata = new Dictionary<string, string>(chunk.Metadata)
            }));
            return Task.CompletedTask;
        }
    }

    private sealed class CountingEmbeddingProvider : IEmbeddingProvider
    {
        public int CallCount { get; private set; }

        public Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new[] { 0.2f, 0.3f, 0.4f });
        }
    }

    private sealed class NoOpDocumentTextExtractor : IDocumentTextExtractor
    {
        public Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NoOpChunkingStrategy : IChunkingStrategy
    {
        public List<DocumentChunkIndexDto> Chunk(IngestDocumentCommand command, DocumentTextExtractionResultDto extraction)
        {
            return new List<DocumentChunkIndexDto>();
        }
    }

    private sealed class NoOpBlobStorageGateway : IBlobStorageGateway
    {
        public Task DeleteAsync(string path, CancellationToken ct) => Task.CompletedTask;

        public Task<Stream> GetAsync(string path, CancellationToken ct)
        {
            return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("conteudo")));
        }

        public Task<string> SaveAsync(Stream content, string path, CancellationToken ct) => Task.FromResult(path);
    }

    private sealed class NoOpPromptInjectionDetector : IPromptInjectionDetector
    {
        public bool TryDetect(string? input, out string pattern)
        {
            pattern = string.Empty;
            return false;
        }
    }

    private sealed class NoOpSecurityAuditLogger : ISecurityAuditLogger
    {
        public void LogAccessDenied(string? userId, string resource)
        {
        }

        public void LogAuthenticationFailure(string? userId, string reason)
        {
        }

        public void LogFileRejected(string fileName, string reason)
        {
        }

        public void LogPromptInjectionDetected(string source, string reason)
        {
        }

        public void LogProviderFallback(string provider, string fallbackProvider, string reason)
        {
        }
    }
}