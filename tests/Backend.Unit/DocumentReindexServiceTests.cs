using Backend.Unit.ChatOrchestratorServiceTestsSupport;
using Backend.Unit.DocumentIngestionServiceTestsSupport;
using Backend.Unit.DocumentReindexServiceTestsSupport;
using Backend.Unit.RetrievalServiceTestsSupport;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using Chatbot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit;

public class DocumentReindexServiceTests
{
    [Fact]
    public async Task ReindexAsync_ShouldMarkDocumentPendingAndQueueJob()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var catalog = new InMemoryDocumentCatalog();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = tenantId,
            Title = "Manual",
            Status = "Indexed",
            Version = 1,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
        });

        var queue = new RecordingBackgroundJobQueue();
        var sut = new DocumentReindexService(
            catalog,
            new FakeRequestContextAccessor { TenantId = tenantId },
            queue,
            new AllowAllDocumentAuthorizationService(),
            new CapturingDocumentReindexSecurityAuditLogger(),
            NullLogger<DocumentReindexService>.Instance);

        var response = await sut.ReindexAsync(documentId, fullReindex: true, CancellationToken.None);

        response.DocumentId.Should().Be(documentId);
        response.Status.Should().Be("ReindexPending");
        response.JobId.Should().NotBeNull();
        queue.EnqueueCount.Should().Be(1);

        var updated = catalog.Get(documentId);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("ReindexPending");
        updated.LastJobId.Should().Be(response.JobId);
    }

    [Fact]
    public async Task ReindexAsync_ShouldDenyAccess_WhenDocumentIsOutsideTenantScope()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var catalog = new InMemoryDocumentCatalog();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = Guid.NewGuid(),
            Title = "Restrito",
            Status = "Indexed",
            Version = 1,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        var audit = new CapturingDocumentReindexSecurityAuditLogger();

        var sut = new DocumentReindexService(
            catalog,
            new FakeRequestContextAccessor { TenantId = tenantId, UserId = "user-123" },
            new RecordingBackgroundJobQueue(),
            new DenyAllDocumentAuthorizationService(),
            audit,
            NullLogger<DocumentReindexService>.Instance);

        var act = () => sut.ReindexAsync(documentId, fullReindex: false, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        audit.LastAccessDeniedResource.Should().Be($"document:{documentId}");
        audit.LastAccessDeniedUserId.Should().Be("user-123");
    }

    [Fact]
    public async Task ReindexAsyncBulk_ShouldSelectOnlyCurrentTenantActiveDocuments_WhenIncludeAllIsEnabled()
    {
        var tenantId = Guid.NewGuid();
        var includedA = Guid.NewGuid();
        var includedB = Guid.NewGuid();
        var excludedDeleted = Guid.NewGuid();
        var excludedOtherTenant = Guid.NewGuid();
        var catalog = new InMemoryDocumentCatalog();
        catalog.Upsert(new DocumentCatalogEntry { DocumentId = includedA, TenantId = tenantId, Title = "A", Status = "Indexed", Version = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        catalog.Upsert(new DocumentCatalogEntry { DocumentId = includedB, TenantId = tenantId, Title = "B", Status = "Failed", Version = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        catalog.Upsert(new DocumentCatalogEntry { DocumentId = excludedDeleted, TenantId = tenantId, Title = "C", Status = "Deleted", Version = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        catalog.Upsert(new DocumentCatalogEntry { DocumentId = excludedOtherTenant, TenantId = Guid.NewGuid(), Title = "D", Status = "Indexed", Version = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        var queue = new RecordingBackgroundJobQueue();

        var sut = new DocumentReindexService(
            catalog,
            new FakeRequestContextAccessor { TenantId = tenantId },
            queue,
            new AllowAllDocumentAuthorizationService(),
            new CapturingDocumentReindexSecurityAuditLogger(),
            NullLogger<DocumentReindexService>.Instance);

        var response = await sut.ReindexAsync(new BulkReindexRequestDto
        {
            IncludeAllTenantDocuments = true,
            Mode = "full"
        }, tenantId, CancellationToken.None);

        response.Accepted.Should().BeTrue();
        response.DocumentCount.Should().Be(2);
        queue.EnqueueCount.Should().Be(2);
        catalog.Get(includedA)!.Status.Should().Be("ReindexPending");
        catalog.Get(includedB)!.Status.Should().Be("ReindexPending");
        catalog.Get(excludedDeleted)!.Status.Should().Be("Deleted");
        catalog.Get(excludedOtherTenant)!.Status.Should().Be("Indexed");
    }
}