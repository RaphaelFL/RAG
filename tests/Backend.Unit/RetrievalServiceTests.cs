using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

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
            new InMemoryDocumentCatalogStub(),
            new AllowAllDocumentAuthorizationService(),
            requestContext,
            new StaticFeatureFlagService(),
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
    public async Task QueryAsync_ShouldMapRetrievedChunks_ToSearchItems()
    {
        var gateway = new CapturingSearchIndexGateway
        {
            Results = new List<SearchResultDto>
            {
                new()
                {
                    ChunkId = "chunk-001",
                    DocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Content = "Conteudo relevante sobre reembolso.",
                    Score = 0.91,
                    Metadata = new Dictionary<string, string>
                    {
                        ["title"] = "Politica Financeira"
                    }
                }
            }
        };

        var catalog = new InMemoryDocumentCatalogStub();
        catalog.Upsert(new DocumentCatalogEntry
        {
            DocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            TenantId = Guid.Empty,
            Title = "Politica Financeira"
        });

        var sut = new RetrievalService(
            gateway,
            catalog,
            new AllowAllDocumentAuthorizationService(),
            new TestRequestContextAccessor(),
            new StaticFeatureFlagService(),
            NullLogger<RetrievalService>.Instance);

        var result = await sut.QueryAsync(new SearchQueryRequestDto
        {
            Query = "reembolso",
            Top = 3
        }, CancellationToken.None);

        result.Count.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items[0].Title.Should().Be("Politica Financeira");
        result.Items[0].ChunkId.Should().Be("chunk-001");
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
            catalog,
            new AllowAllDocumentAuthorizationService(),
            new TestRequestContextAccessor { TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111") },
            new StaticFeatureFlagService { IsSemanticRankingEnabled = false },
            NullLogger<RetrievalService>.Instance);

        var result = await sut.RetrieveAsync(new RetrievalQueryDto
        {
            Query = "manual",
            SemanticRanking = true
        }, CancellationToken.None);

        result.RetrievalStrategy.Should().Be("hybrid");
    }

    private sealed class TestRequestContextAccessor : IRequestContextAccessor
    {
        public Guid? TenantId { get; set; }
        public string? UserId { get; set; }
        public string? UserRole { get; set; }
    }

    private sealed class StaticFeatureFlagService : IFeatureFlagService
    {
        public bool IsSemanticRankingEnabled { get; set; } = true;
        public bool IsGraphRagEnabled => false;
        public bool IsMcpEnabled => false;
    }

    private sealed class AllowAllDocumentAuthorizationService : IDocumentAuthorizationService
    {
        public bool CanAccess(DocumentCatalogEntry document, Guid? tenantId, string? userId, string? userRole) => true;
    }

    private sealed class InMemoryDocumentCatalogStub : IDocumentCatalog
    {
        private readonly Dictionary<Guid, DocumentCatalogEntry> _entries = new();

        public void Upsert(DocumentCatalogEntry entry) => _entries[entry.DocumentId] = entry;

        public DocumentCatalogEntry? Get(Guid documentId) => _entries.TryGetValue(documentId, out var entry) ? entry : null;

        public IReadOnlyCollection<DocumentCatalogEntry> Query(FileSearchFilterDto? filters) => _entries.Values.ToList();

        public DocumentCatalogEntry? FindByContentHash(Guid tenantId, string contentHash) => null;
    }

    private sealed class CapturingSearchIndexGateway : ISearchIndexGateway
    {
        public FileSearchFilterDto? LastFilters { get; private set; }

        public List<SearchResultDto> Results { get; set; } = new();

        public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task<List<SearchResultDto>> HybridSearchAsync(string query, int topK, FileSearchFilterDto? filters, CancellationToken ct)
        {
            LastFilters = filters;
            return Task.FromResult(Results.Take(topK).ToList());
        }

        public Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}