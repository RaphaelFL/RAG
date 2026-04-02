using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Authentication;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence;

public sealed class AzureSearchIndexGateway : ISearchIndexGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly SearchOptions _searchOptions;
    private readonly ExternalProviderClientOptions _providerOptions;
    private readonly IAzureAccessTokenProvider _azureAccessTokenProvider;
    private readonly SemaphoreSlim _indexInitializationLock = new(1, 1);
    private volatile bool _indexInitialized;

    public AzureSearchIndexGateway(
        IHttpClientFactory httpClientFactory,
        IOptions<EmbeddingOptions> embeddingOptions,
        IOptions<SearchOptions> searchOptions,
        IOptions<ExternalProviderClientOptions> providerOptions,
        IAzureAccessTokenProvider azureAccessTokenProvider)
    {
        _httpClientFactory = httpClientFactory;
        _embeddingOptions = embeddingOptions.Value;
        _searchOptions = searchOptions.Value;
        _providerOptions = providerOptions.Value;
        _azureAccessTokenProvider = azureAccessTokenProvider;
    }

    public async Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        await EnsureIndexExistsAsync(ct);
        var client = await CreateClientAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/indexes/{Uri.EscapeDataString(_searchOptions.IndexName)}/docs/index?api-version={Uri.EscapeDataString(_providerOptions.AzureSearchApiVersion)}");
        request.Content = JsonContent.Create(new
        {
            value = chunks.Select(chunk => BuildUploadDocument(chunk)).ToArray()
        });

        using var response = await client.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Azure AI Search indexing failed with status {(int)response.StatusCode}: {payload}");
        }
    }

    public async Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        await EnsureIndexExistsAsync(ct);
        var client = await CreateClientAsync(ct);
        var requestBody = new Dictionary<string, object?>
        {
            ["search"] = string.IsNullOrWhiteSpace(query) ? "*" : query,
            ["top"] = topK,
            ["filter"] = BuildFilter(filters),
            ["queryType"] = _searchOptions.SemanticRankingEnabled ? "semantic" : "simple",
            ["semanticConfiguration"] = _searchOptions.SemanticConfigurationName,
            ["select"] = "id,chunkId,documentId,content,title,tenantId,tags,categories,pageNumber,endPage,section,category,contentType,source"
        };

        if (queryEmbedding is { Length: > 0 } && _searchOptions.HybridSearchWeight > 0)
        {
            requestBody["vectorQueries"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["kind"] = "vector",
                    ["vector"] = queryEmbedding,
                    ["fields"] = "contentVector",
                    ["k"] = topK,
                    ["weight"] = _searchOptions.HybridSearchWeight
                }
            };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/indexes/{Uri.EscapeDataString(_searchOptions.IndexName)}/docs/search?api-version={Uri.EscapeDataString(_providerOptions.AzureSearchApiVersion)}");
        request.Content = JsonContent.Create(requestBody);

        using var response = await client.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Azure AI Search query failed with status {(int)response.StatusCode}: {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("value", out var values))
        {
            return new List<SearchResultDto>();
        }

        return values.EnumerateArray().Select(MapResult).ToList();
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
    {
        await EnsureIndexExistsAsync(ct);
        var matches = await FindChunkKeysAsync(documentId, ct);
        if (matches.Count == 0)
        {
            return;
        }

        var client = await CreateClientAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/indexes/{Uri.EscapeDataString(_searchOptions.IndexName)}/docs/index?api-version={Uri.EscapeDataString(_providerOptions.AzureSearchApiVersion)}");
        request.Content = JsonContent.Create(new
        {
            value = matches.Select(id => new Dictionary<string, object?>
            {
                ["@search.action"] = "delete",
                ["id"] = id
            }).ToArray()
        });

        using var response = await client.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Azure AI Search delete failed with status {(int)response.StatusCode}: {payload}");
        }
    }

    private async Task<HttpClient> CreateClientAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("AzureSearch");
        client.DefaultRequestHeaders.Remove("api-key");
        client.DefaultRequestHeaders.Remove("Authorization");

        if (ExternalProviderClientOptions.HasConfiguredValue(_providerOptions.AzureSearchApiKey))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("api-key", _providerOptions.AzureSearchApiKey);
            return client;
        }

        var token = await _azureAccessTokenProvider.GetTokenAsync("https://search.azure.com/.default", ct);
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private async Task EnsureIndexExistsAsync(CancellationToken ct)
    {
        if (_indexInitialized)
        {
            return;
        }

        await _indexInitializationLock.WaitAsync(ct);
        try
        {
            if (_indexInitialized)
            {
                return;
            }

            var client = await CreateClientAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/indexes/{Uri.EscapeDataString(_searchOptions.IndexName)}?allowIndexDowntime=true&api-version={Uri.EscapeDataString(_providerOptions.AzureSearchApiVersion)}");
            request.Content = JsonContent.Create(new
            {
                name = _searchOptions.IndexName,
                fields = new object[]
                {
                    new { name = "id", type = "Edm.String", key = true, searchable = false, filterable = true, sortable = false, facetable = false },
                    new { name = "chunkId", type = "Edm.String", searchable = false, filterable = true, sortable = false, facetable = false },
                    new { name = "documentId", type = "Edm.String", searchable = false, filterable = true, sortable = false, facetable = false },
                    new { name = "content", type = "Edm.String", searchable = true, filterable = false, sortable = false, facetable = false },
                    new { name = "title", type = "Edm.String", searchable = true, filterable = false, sortable = false, facetable = false },
                    new { name = "tenantId", type = "Edm.String", searchable = false, filterable = true, sortable = false, facetable = false },
                    new { name = "tags", type = "Collection(Edm.String)", searchable = true, filterable = true, sortable = false, facetable = true },
                    new { name = "categories", type = "Collection(Edm.String)", searchable = true, filterable = true, sortable = false, facetable = true },
                    new { name = "pageNumber", type = "Edm.Int32", searchable = false, filterable = true, sortable = true, facetable = false },
                    new { name = "endPage", type = "Edm.Int32", searchable = false, filterable = true, sortable = true, facetable = false },
                    new { name = "section", type = "Edm.String", searchable = true, filterable = true, sortable = false, facetable = false },
                    new { name = "category", type = "Edm.String", searchable = true, filterable = true, sortable = false, facetable = true },
                    new { name = "contentType", type = "Edm.String", searchable = false, filterable = true, sortable = false, facetable = true },
                    new { name = "source", type = "Edm.String", searchable = true, filterable = true, sortable = false, facetable = true },
                    new { name = "contentVector", type = "Collection(Edm.Single)", searchable = true, retrievable = false, stored = false, dimensions = _embeddingOptions.Dimensions, vectorSearchProfile = "content-vector-profile" }
                },
                semantic = new
                {
                    configurations = new[]
                    {
                        new
                        {
                            name = _searchOptions.SemanticConfigurationName,
                            prioritizedFields = new
                            {
                                titleField = new { fieldName = "title" },
                                contentFields = new[] { new { fieldName = "content" } }
                            }
                        }
                    }
                },
                vectorSearch = new
                {
                    algorithms = new[]
                    {
                        new
                        {
                            name = "content-vector-hnsw",
                            kind = "hnsw",
                            hnswParameters = new
                            {
                                m = 4,
                                efConstruction = 400,
                                efSearch = 500,
                                metric = "cosine"
                            }
                        }
                    },
                    profiles = new[]
                    {
                        new
                        {
                            name = "content-vector-profile",
                            algorithm = "content-vector-hnsw"
                        }
                    }
                }
            });

            using var response = await client.SendAsync(request, ct);
            var payload = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Azure AI Search index provisioning failed with status {(int)response.StatusCode}: {payload}");
            }

            _indexInitialized = true;
        }
        finally
        {
            _indexInitializationLock.Release();
        }
    }

    private static Dictionary<string, object?> BuildUploadDocument(DocumentChunkIndexDto chunk)
    {
        chunk.Metadata.TryGetValue("title", out var title);
        chunk.Metadata.TryGetValue("tenantId", out var tenantId);
        chunk.Metadata.TryGetValue("tags", out var tags);
        chunk.Metadata.TryGetValue("categories", out var categories);
        chunk.Metadata.TryGetValue("category", out var category);
        chunk.Metadata.TryGetValue("contentType", out var contentType);
        chunk.Metadata.TryGetValue("source", out var source);
        var endPage = TryParseMetadataInt(chunk.Metadata, "endPage", chunk.PageNumber);

        return new Dictionary<string, object?>
        {
            ["@search.action"] = "mergeOrUpload",
            ["id"] = chunk.ChunkId,
            ["chunkId"] = chunk.ChunkId,
            ["documentId"] = chunk.DocumentId.ToString(),
            ["content"] = chunk.Content,
            ["title"] = title ?? string.Empty,
            ["tenantId"] = tenantId ?? string.Empty,
            ["tags"] = Split(tags),
            ["categories"] = Split(categories),
            ["pageNumber"] = chunk.PageNumber,
            ["endPage"] = endPage,
            ["section"] = chunk.Section,
            ["category"] = category ?? string.Empty,
            ["contentType"] = contentType ?? string.Empty,
            ["source"] = source ?? string.Empty,
            ["contentVector"] = chunk.Embedding is { Length: > 0 } ? chunk.Embedding : null
        };
    }

    private async Task<List<string>> FindChunkKeysAsync(Guid documentId, CancellationToken ct)
    {
        var client = await CreateClientAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/indexes/{Uri.EscapeDataString(_searchOptions.IndexName)}/docs/search?api-version={Uri.EscapeDataString(_providerOptions.AzureSearchApiVersion)}");
        request.Content = JsonContent.Create(new
        {
            search = "*",
            filter = $"documentId eq '{documentId}'",
            top = 1000,
            select = "id"
        });

        using var response = await client.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Azure AI Search lookup failed with status {(int)response.StatusCode}: {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("value", out var values)
            ? values.EnumerateArray().Select(item => item.GetProperty("id").GetString() ?? string.Empty).Where(id => !string.IsNullOrWhiteSpace(id)).ToList()
            : new List<string>();
    }

    private static SearchResultDto MapResult(JsonElement item)
    {
        var tags = item.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array
            ? string.Join(',', tagsElement.EnumerateArray().Select(value => value.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)))
            : string.Empty;
        var categories = item.TryGetProperty("categories", out var categoriesElement) && categoriesElement.ValueKind == JsonValueKind.Array
            ? string.Join(',', categoriesElement.EnumerateArray().Select(value => value.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)))
            : string.Empty;

        return new SearchResultDto
        {
            ChunkId = item.GetProperty("chunkId").GetString() ?? string.Empty,
            DocumentId = Guid.Parse(item.GetProperty("documentId").GetString() ?? Guid.Empty.ToString()),
            Content = item.GetProperty("content").GetString() ?? string.Empty,
            Score = item.TryGetProperty("@search.score", out var scoreElement) ? scoreElement.GetDouble() : 0,
            Metadata = new Dictionary<string, string>
            {
                ["title"] = item.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? string.Empty : string.Empty,
                ["tenantId"] = item.TryGetProperty("tenantId", out var tenantElement) ? tenantElement.GetString() ?? string.Empty : string.Empty,
                ["tags"] = tags,
                ["categories"] = categories,
                ["startPage"] = item.TryGetProperty("pageNumber", out var pageElement) ? pageElement.GetInt32().ToString() : "1",
                ["endPage"] = item.TryGetProperty("endPage", out var endPageElement) ? endPageElement.GetInt32().ToString() : (item.TryGetProperty("pageNumber", out var fallbackPageElement) ? fallbackPageElement.GetInt32().ToString() : "1"),
                ["section"] = item.TryGetProperty("section", out var sectionElement) ? sectionElement.GetString() ?? string.Empty : string.Empty,
                ["category"] = item.TryGetProperty("category", out var categoryElement) ? categoryElement.GetString() ?? string.Empty : string.Empty,
                ["contentType"] = item.TryGetProperty("contentType", out var contentTypeElement) ? contentTypeElement.GetString() ?? string.Empty : string.Empty,
                ["source"] = item.TryGetProperty("source", out var sourceElement) ? sourceElement.GetString() ?? string.Empty : string.Empty
            }
        };
    }

    private static string? BuildFilter(FileSearchFilterDto? filters)
    {
        if (filters is null)
        {
            return null;
        }

        var clauses = new List<string>();
        if (filters.TenantId.HasValue)
        {
            clauses.Add($"tenantId eq '{filters.TenantId.Value}'");
        }

        if (filters.DocumentIds is { Count: > 0 })
        {
            clauses.Add($"({string.Join(" or ", filters.DocumentIds.Select(id => $"documentId eq '{id}'"))})");
        }

        if (filters.Tags is { Count: > 0 })
        {
            clauses.Add($"({string.Join(" or ", filters.Tags.Select(tag => $"tags/any(t: t eq '{EscapeFilterValue(tag)}')"))})");
        }

        if (filters.Categories is { Count: > 0 })
        {
            clauses.Add($"({string.Join(" or ", filters.Categories.Select(category => $"categories/any(c: c eq '{EscapeFilterValue(category)}')"))})");
        }

        if (filters.ContentTypes is { Count: > 0 })
        {
            clauses.Add($"({string.Join(" or ", filters.ContentTypes.Select(contentType => $"contentType eq '{EscapeFilterValue(contentType)}'"))})");
        }

        if (filters.Sources is { Count: > 0 })
        {
            clauses.Add($"({string.Join(" or ", filters.Sources.Select(source => $"source eq '{EscapeFilterValue(source)}'"))})");
        }

        return clauses.Count == 0 ? null : string.Join(" and ", clauses);
    }

    private static string EscapeFilterValue(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static int TryParseMetadataInt(Dictionary<string, string> metadata, string key, int fallback)
    {
        return metadata.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, out var parsed)
            ? parsed
            : fallback;
    }

    private static string[] Split(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}