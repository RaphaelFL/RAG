using System.Text;
using Backend.Unit.DocumentIngestionServiceTestsSupport;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Services;
using Chatbot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit;

public class DocumentIngestionServiceTests
{
    private static DocumentIngestionService CreateSut(
        RecordingBlobStorageGateway blobGateway,
        StubMalwareScanner malwareScanner,
        InMemoryDocumentCatalog catalog,
        RecordingBackgroundJobQueue queue)
    {
        return new DocumentIngestionService(
            new IngestionContentStorage(blobGateway, catalog),
            malwareScanner,
            catalog,
            new IngestionCatalogEntryFactory(),
            new IngestionJobScheduler(queue),
            NullLogger<DocumentIngestionService>.Instance);
    }

    [Fact]
    public async Task IngestAsync_ShouldQueueDocument_WhenPayloadIsSafe()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var catalog = new InMemoryDocumentCatalog();
        var blobGateway = new RecordingBlobStorageGateway();
        var queue = new RecordingBackgroundJobQueue();
        var sut = CreateSut(blobGateway, new StubMalwareScanner(new MalwareScanResultDto { IsSafe = true }), catalog, queue);

        var result = await sut.IngestAsync(CreateCommand(documentId, tenantId, "conteudo seguro"), CancellationToken.None);

        result.DocumentId.Should().Be(documentId);
        result.Status.Should().Be("Queued");
        queue.EnqueueCount.Should().Be(1);
        blobGateway.SavedPaths.Should().ContainSingle(path => path == $"documents/{tenantId}/{documentId}/raw-content");

        var stored = catalog.Get(documentId);
        stored.Should().NotBeNull();
        stored!.Status.Should().Be("Queued");
        stored.StoragePath.Should().Be($"documents/{tenantId}/{documentId}/raw-content");
        stored.QuarantinePath.Should().BeNull();
    }

    [Fact]
    public async Task IngestAsync_ShouldPersistFailedDocumentAndQuarantine_WhenMalwareRequiresIsolation()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var catalog = new InMemoryDocumentCatalog();
        var blobGateway = new RecordingBlobStorageGateway();
        var queue = new RecordingBackgroundJobQueue();
        var sut = CreateSut(blobGateway, new StubMalwareScanner(new MalwareScanResultDto
            {
                IsSafe = false,
                RequiresQuarantine = true,
                Reason = "malware detected"
            }), catalog, queue);

        var act = () => sut.IngestAsync(CreateCommand(documentId, tenantId, "payload suspeito"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("malware detected");
        queue.EnqueueCount.Should().Be(0);
        blobGateway.SavedPaths.Should().Contain($"documents/{tenantId}/{documentId}/raw-content");
        blobGateway.SavedPaths.Should().Contain($"quarantine/{tenantId}/{documentId}/manual.pdf");

        var stored = catalog.Get(documentId);
        stored.Should().NotBeNull();
        stored!.Status.Should().Be("Failed");
        stored.QuarantinePath.Should().Be($"quarantine/{tenantId}/{documentId}/manual.pdf");
    }

    [Fact]
    public async Task IngestAsync_ShouldRejectDuplicate_WhenSameContentAlreadyExistsForTenant()
    {
        var tenantId = Guid.NewGuid();
        var payload = Encoding.UTF8.GetBytes("conteudo duplicado");
        var catalog = new InMemoryDocumentCatalog();
        var existingDocumentId = Guid.NewGuid();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = existingDocumentId,
            TenantId = tenantId,
            Title = "Documento existente",
            OriginalFileName = "existente.pdf",
            ContentType = "application/pdf",
            Status = "Indexed",
            Version = 1,
            ContentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            Chunks = new List<DocumentChunkIndexDto>()
        });

        var sut = CreateSut(
            new RecordingBlobStorageGateway(),
            new StubMalwareScanner(new MalwareScanResultDto { IsSafe = true }),
            catalog,
            new RecordingBackgroundJobQueue());

        var act = () => sut.IngestAsync(CreateCommand(Guid.NewGuid(), tenantId, "conteudo duplicado"), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateDocumentException>();
    }

    private static IngestDocumentCommand CreateCommand(Guid documentId, Guid tenantId, string content)
    {
        return new IngestDocumentCommand
        {
            DocumentId = documentId,
            TenantId = tenantId,
            FileName = "manual.pdf",
            ContentType = "application/pdf",
            ContentLength = Encoding.UTF8.GetByteCount(content),
            DocumentTitle = "Manual",
            Content = new MemoryStream(Encoding.UTF8.GetBytes(content), writable: false)
        };
    }
}