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
            new StaticEmbeddingProvider(),
            catalog,
            new AllowAllDocumentAuthorizationService(),
            new TestRequestContextAccessor(),
            new InMemoryApplicationCache(),
            new StaticFeatureFlagService(),
            new StaticRagRuntimeSettings(),
            new NoOpOperationalAuditStore(),
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

    private sealed class StaticRagRuntimeSettings : IRagRuntimeSettings
    {
        public int DenseChunkSize => 420;
        public int DenseOverlap => 48;
        public int NarrativeChunkSize => 900;
        public int NarrativeOverlap => 96;
        public int MinimumChunkCharacters => 120;
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

    private sealed class StaticEmbeddingProvider : IEmbeddingProvider
    {
        private readonly float[] _embedding;

        public StaticEmbeddingProvider(float[]? embedding = null)
        {
            _embedding = embedding ?? new float[] { 0.25f, 0.5f, 0.75f };
        }

        public Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
        {
            return Task.FromResult(_embedding);
        }
    }

    private sealed class InMemoryApplicationCache : IApplicationCache
    {
        private readonly Dictionary<string, object> _entries = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken ct)
        {
            return Task.FromResult(_entries.TryGetValue(key, out var value) ? (T?)value : default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
        {
            _entries[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
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

        public float[]? LastQueryEmbedding { get; private set; }

        public List<SearchResultDto> Results { get; set; } = new();

        public int CallCount { get; private set; }

        public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct)
        {
            return Task.FromResult(new List<DocumentChunkIndexDto>());
        }

        public Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
        {
            LastFilters = filters;
            LastQueryEmbedding = queryEmbedding;
            CallCount++;
            return Task.FromResult(Results.Take(topK).ToList());
        }

        public Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}