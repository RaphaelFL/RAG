using Backend.Unit.ChatOrchestratorServiceTestsSupport;
using Backend.Unit.DocumentReindexServiceTestsSupport;
using Backend.Unit.RetrievalServiceTestsSupport;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Services;
using Chatbot.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace Backend.Unit;

public class DocumentQueryServiceTests
{
    [Fact]
    public async Task GetDocumentInspectionAsync_ShouldReturnPagedInspection_WhenDocumentIsAccessible()
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
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        var gateway = new CapturingSearchIndexGateway
        {
            DocumentChunks = new List<DocumentChunkIndexDto>
            {
                new()
                {
                    ChunkId = "chunk-001",
                    DocumentId = documentId,
                    Content = "Primeiro trecho",
                    PageNumber = 1,
                    Metadata = new Dictionary<string, string> { ["chunkIndex"] = "0" }
                },
                new()
                {
                    ChunkId = "chunk-002",
                    DocumentId = documentId,
                    Content = "Segundo trecho",
                    PageNumber = 2,
                    Metadata = new Dictionary<string, string> { ["chunkIndex"] = "1" }
                }
            }
        };

        var sut = new DocumentQueryService(
            catalog,
            new FakeRequestContextAccessor { TenantId = tenantId },
            new AllowAllDocumentAuthorizationService(),
            gateway,
            new CapturingDocumentReindexSecurityAuditLogger());

        var result = await sut.GetDocumentInspectionAsync(documentId, null, 1, 1, CancellationToken.None);

        result.Should().NotBeNull();
        result!.TotalChunkCount.Should().Be(2);
        result.Chunks.Should().HaveCount(1);
        result.PageNumber.Should().Be(1);
        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task GetDocumentAsync_ShouldThrowUnauthorized_WhenDocumentIsOutsideTenantScope()
    {
        var documentId = Guid.NewGuid();
        var catalog = new InMemoryDocumentCatalog();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = Guid.NewGuid(),
            Title = "Restrito",
            Status = "Indexed",
            Version = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        var sut = new DocumentQueryService(
            catalog,
            new FakeRequestContextAccessor { TenantId = Guid.NewGuid(), UserId = "user-xyz" },
            new DenyAllDocumentAuthorizationService(),
            new CapturingSearchIndexGateway(),
            new CapturingDocumentReindexSecurityAuditLogger());

        var act = () => sut.GetDocumentAsync(documentId, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetOriginalDocumentAsync_ShouldReturnStorageReference_WhenDocumentIsAccessible()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var catalog = new InMemoryDocumentCatalog();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = tenantId,
            Title = "Manual",
            OriginalFileName = "manual.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            StoragePath = $"documents/{tenantId}/{documentId}/raw-content",
            Status = "Indexed",
            Version = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        var sut = new DocumentQueryService(
            catalog,
            new FakeRequestContextAccessor { TenantId = tenantId },
            new AllowAllDocumentAuthorizationService(),
            new CapturingSearchIndexGateway(),
            new CapturingDocumentReindexSecurityAuditLogger());

        var result = await sut.GetOriginalDocumentAsync(documentId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.DocumentId.Should().Be(documentId);
        result.OriginalFileName.Should().Be("manual.docx");
        result.StoragePath.Should().Be($"documents/{tenantId}/{documentId}/raw-content");
    }

    [Fact]
    public async Task GetDocumentTextPreviewAsync_ShouldMergeChunkOverlap_WhenDocumentIsAccessible()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var catalog = new InMemoryDocumentCatalog();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = tenantId,
            Title = "Documento consolidado",
            OriginalFileName = "documento.docx",
            Status = "Indexed",
            Version = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        var gateway = new CapturingSearchIndexGateway
        {
            DocumentChunks = new List<DocumentChunkIndexDto>
            {
                new()
                {
                    ChunkId = "chunk-001",
                    DocumentId = documentId,
                    Content = "DDD - Domain Driven Design\nObjetivo principal do projeto",
                    PageNumber = 1,
                    Metadata = new Dictionary<string, string> { ["chunkIndex"] = "0" }
                },
                new()
                {
                    ChunkId = "chunk-002",
                    DocumentId = documentId,
                    Content = "Objetivo principal do projeto\nUma plataforma gamificada para investir.",
                    PageNumber = 1,
                    Metadata = new Dictionary<string, string> { ["chunkIndex"] = "1" }
                }
            }
        };

        var sut = new DocumentQueryService(
            catalog,
            new FakeRequestContextAccessor { TenantId = tenantId },
            new AllowAllDocumentAuthorizationService(),
            gateway,
            new CapturingDocumentReindexSecurityAuditLogger());

        var result = await sut.GetDocumentTextPreviewAsync(documentId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ChunkCount.Should().Be(2);
        result.Content.Should().Be("DDD - Domain Driven Design\nObjetivo principal do projeto\nUma plataforma gamificada para investir.");
    }
}