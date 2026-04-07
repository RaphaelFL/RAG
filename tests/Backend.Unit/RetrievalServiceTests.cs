using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

using Backend.Unit.RetrievalServiceTestsSupport;

namespace Backend.Unit;

public class RetrievalServiceTests
{
    [Fact]
    public async Task RetrieveAsync_ShouldAlwaysApplyTenantFilter_WhenTenantContextExists()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var gateway = new CapturingSearchIndexGateway();
        var requestContext = new TestRequestContextAccessor { TenantId = tenantId };
        var sut = new RetrievalService(
            gateway,
            new StaticEmbeddingProvider(),
            new InMemoryDocumentCatalogStub(),
            new AllowAllDocumentAuthorizationService(),
            requestContext,
            new InMemoryApplicationCache(),
            new StaticFeatureFlagService(),
            new StaticRagRuntimeSettings(),
            new NoOpOperationalAuditStore(),
            NullLogger<RetrievalService>.Instance);

        await sut.RetrieveAsync(new RetrievalQueryDto
        {
            Query = "politica de reembolso",
            TopK = 5
        }, CancellationToken.None);

        gateway.LastFilters.Should().NotBeNull();
        gateway.LastFilters!.TenantId.Should().Be(tenantId);
        gateway.LastFilters.DocumentIds.Should().BeNull();
        gateway.LastFilters.Tags.Should().BeNull();
        gateway.LastFilters.Categories.Should().BeNull();
    }

    [Fact]
    public async Task RetrieveAsync_ShouldReportHybridStrategy_WhenSemanticRankingFlagIsDisabled()
    {
        var documentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var gateway = new CapturingSearchIndexGateway
        {
            Results = new List<SearchResultDto>
            {
                new()
                {
                    ChunkId = "chunk-002",
                    DocumentId = documentId,
                    Content = "Conteudo relevante",
                    Score = 0.8,
                    Metadata = new Dictionary<string, string>
                    {
                        ["title"] = "Manual"
                    }
                }
            }
        };
        var catalog = new InMemoryDocumentCatalogStub();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Title = "Manual"
        });

        var sut = new RetrievalService(
            gateway,
            new StaticEmbeddingProvider(),
            catalog,
            new AllowAllDocumentAuthorizationService(),
            new TestRequestContextAccessor { TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111") },
            new InMemoryApplicationCache(),
            new StaticFeatureFlagService { IsSemanticRankingEnabled = false },
            new StaticRagRuntimeSettings(),
            new NoOpOperationalAuditStore(),
            NullLogger<RetrievalService>.Instance);

        var result = await sut.RetrieveAsync(new RetrievalQueryDto
        {
            Query = "manual",
            SemanticRanking = true
        }, CancellationToken.None);

        result.RetrievalStrategy.Should().Be("hybrid-reranked");
    }

    [Fact]
    public async Task RetrieveAsync_ShouldUseCache_OnIdenticalQueries()
    {
        var documentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var gateway = new CapturingSearchIndexGateway
        {
            Results = new List<SearchResultDto>
            {
                new()
                {
                    ChunkId = "chunk-003",
                    DocumentId = documentId,
                    Content = "Politica de viagens corporativas atualizada.",
                    Score = 0.83,
                    Metadata = new Dictionary<string, string>
                    {
                        ["title"] = "Politica de Viagens"
                    }
                }
            }
        };
        var catalog = new InMemoryDocumentCatalogStub();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Title = "Politica de Viagens"
        });

        var sut = new RetrievalService(
            gateway,
            new StaticEmbeddingProvider(),
            catalog,
            new AllowAllDocumentAuthorizationService(),
            new TestRequestContextAccessor { TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111") },
            new InMemoryApplicationCache(),
            new StaticFeatureFlagService(),
            new StaticRagRuntimeSettings(),
            new NoOpOperationalAuditStore(),
            NullLogger<RetrievalService>.Instance);

        await sut.RetrieveAsync(new RetrievalQueryDto
        {
            Query = "politica viagens",
            TopK = 3
        }, CancellationToken.None);

        await sut.RetrieveAsync(new RetrievalQueryDto
        {
            Query = "politica viagens",
            TopK = 3
        }, CancellationToken.None);

        gateway.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldMapPageAndSectionMetadata()
    {
        var documentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var gateway = new CapturingSearchIndexGateway
        {
            Results = new List<SearchResultDto>
            {
                new()
                {
                    ChunkId = "chunk-004",
                    DocumentId = documentId,
                    Content = "Fluxo detalhado do processo.",
                    Score = 0.88,
                    Metadata = new Dictionary<string, string>
                    {
                        ["title"] = "Manual Operacional",
                        ["startPage"] = "3",
                        ["endPage"] = "4",
                        ["section"] = "Procedimento"
                    }
                }
            }
        };
        var catalog = new InMemoryDocumentCatalogStub();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Title = "Manual Operacional"
        });

        var sut = new RetrievalService(
            gateway,
            new StaticEmbeddingProvider(),
            catalog,
            new AllowAllDocumentAuthorizationService(),
            new TestRequestContextAccessor { TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111") },
            new InMemoryApplicationCache(),
            new StaticFeatureFlagService(),
            new StaticRagRuntimeSettings(),
            new NoOpOperationalAuditStore(),
            NullLogger<RetrievalService>.Instance);

        var result = await sut.RetrieveAsync(new RetrievalQueryDto
        {
            Query = "processo",
            TopK = 1
        }, CancellationToken.None);

        result.Chunks.Should().ContainSingle();
        result.Chunks[0].PageNumber.Should().Be(3);
        result.Chunks[0].Section.Should().Be("Procedimento");
    }

    [Fact]
    public async Task RetrieveAsync_ShouldPassQueryEmbeddingToGateway()
    {
        var documentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var gateway = new CapturingSearchIndexGateway
        {
            Results = new List<SearchResultDto>
            {
                new()
                {
                    ChunkId = "chunk-005",
                    DocumentId = documentId,
                    Content = "Resumo sobre reembolso internacional.",
                    Score = 0.79,
                    Metadata = new Dictionary<string, string>
                    {
                        ["title"] = "Guia Global"
                    }
                }
            }
        };
        var catalog = new InMemoryDocumentCatalogStub();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = Guid.Empty,
            Title = "Guia Global"
        });
        var embeddingProvider = new StaticEmbeddingProvider(new float[] { 0.11f, 0.22f, 0.33f });

        var sut = new RetrievalService(
            gateway,
            embeddingProvider,
            catalog,
            new AllowAllDocumentAuthorizationService(),
            new TestRequestContextAccessor(),
            new InMemoryApplicationCache(),
            new StaticFeatureFlagService(),
            new StaticRagRuntimeSettings(),
            new NoOpOperationalAuditStore(),
            NullLogger<RetrievalService>.Instance);

        await sut.RetrieveAsync(new RetrievalQueryDto
        {
            Query = "reembolso internacional",
            TopK = 1
        }, CancellationToken.None);

        gateway.LastQueryEmbedding.Should().Equal(0.11f, 0.22f, 0.33f);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldInvalidateCache_WhenScopedDocumentVersionChanges()
    {
        var tenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var documentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var gateway = new CapturingSearchIndexGateway
        {
            Results = new List<SearchResultDto>
            {
                new()
                {
                    ChunkId = "chunk-006",
                    DocumentId = documentId,
                    Content = "Versao inicial da politica.",
                    Score = 0.8,
                    Metadata = new Dictionary<string, string>
                    {
                        ["title"] = "Politica Viva"
                    }
                }
            }
        };
        var catalog = new InMemoryDocumentCatalogStub();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = tenantId,
            Title = "Politica Viva",
            Version = 1,
            UpdatedAtUtc = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc)
        });
        var sut = new RetrievalService(
            gateway,
            new StaticEmbeddingProvider(),
            catalog,
            new AllowAllDocumentAuthorizationService(),
            new TestRequestContextAccessor { TenantId = tenantId },
            new InMemoryApplicationCache(),
            new StaticFeatureFlagService(),
            new StaticRagRuntimeSettings(),
            new NoOpOperationalAuditStore(),
            NullLogger<RetrievalService>.Instance);

        var query = new RetrievalQueryDto
        {
            Query = "politica",
            TopK = 1
        };

        await sut.RetrieveAsync(query, CancellationToken.None);

        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = documentId,
            TenantId = tenantId,
            Title = "Politica Viva",
            Version = 2,
            UpdatedAtUtc = new DateTime(2026, 4, 1, 12, 5, 0, DateTimeKind.Utc)
        });

        await sut.RetrieveAsync(query, CancellationToken.None);

        gateway.CallCount.Should().Be(2);
    }








}
