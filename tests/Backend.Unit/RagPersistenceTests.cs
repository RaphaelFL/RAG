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

using Backend.Unit.RagPersistenceTestsSupport;

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








}
