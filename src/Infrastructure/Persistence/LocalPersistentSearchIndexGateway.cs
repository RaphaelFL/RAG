using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence;

public sealed class LocalPersistentSearchIndexGateway : ISearchIndexGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _sync = new();
    private readonly string _indexFilePath;
    private readonly IDocumentCatalog _documentCatalog;
    private Dictionary<string, IndexedChunk> _index;

    public LocalPersistentSearchIndexGateway(IDocumentCatalog documentCatalog, IOptions<LocalPersistenceOptions> options, IHostEnvironment environment)
    {
        _documentCatalog = documentCatalog;
        var basePath = ResolveBasePath(options.Value.BasePath, environment.ContentRootPath);
        Directory.CreateDirectory(basePath);
        _indexFilePath = Path.Combine(basePath, options.Value.SearchIndexFileName);
        _index = Load();
    }

    public Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
    {
        lock (_sync)
        {
            foreach (var chunk in chunks)
            {
                _index[chunk.ChunkId] = IndexedChunk.From(chunk);
            }

            Persist();
        }

        return Task.CompletedTask;
    }

    public Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct)
    {
        List<DocumentChunkIndexDto> chunks;
        lock (_sync)
        {
            chunks = _index.Values
                .Where(item => item.DocumentId == documentId)
                .OrderBy(item => item.GetChunkIndex())
                .ThenBy(item => item.ChunkId, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.ToDocumentChunk())
                .ToList();
        }

        if (chunks.Count > 0)
        {
            return Task.FromResult(chunks);
        }

        var legacyDocument = _documentCatalog.Get(documentId);
        var legacyChunks = legacyDocument?.Chunks
            .Select(CloneChunk)
            .ToList()
            ?? new List<DocumentChunkIndexDto>();

        return Task.FromResult(legacyChunks);
    }

    public Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        List<IndexedChunk> candidateResults;
        lock (_sync)
        {
            candidateResults = _index.Values.Where(result => MatchesFilters(result, filters)).Select(IndexedChunk.Clone).ToList();
        }

        if (candidateResults.Count == 0)
        {
            candidateResults = BuildFallbackResults(filters);
        }

        var orderedResults = candidateResults
            .Select(result => new SearchResultDto
            {
                ChunkId = result.ChunkId,
                DocumentId = result.DocumentId,
                Content = result.Content,
                Metadata = new Dictionary<string, string>(result.Metadata),
                Score = CalculateScore(query, queryEmbedding, result)
            })
            .OrderByDescending(result => result.Score)
            .Take(topK)
            .ToList();

        return Task.FromResult(orderedResults);
    }

    public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
    {
        lock (_sync)
        {
            var idsToRemove = _index.Values
                .Where(item => item.DocumentId == documentId)
                .Select(item => item.ChunkId)
                .ToList();

            foreach (var chunkId in idsToRemove)
            {
                _index.Remove(chunkId);
            }

            Persist();
        }

        return Task.CompletedTask;
    }

    private List<IndexedChunk> BuildFallbackResults(FileSearchFilterDto? filters)
    {
        return _documentCatalog.Query(filters)
            .SelectMany(document => document.Chunks)
            .Select(chunk => IndexedChunk.From(chunk, 0.9))
            .ToList();
    }

    private static bool MatchesFilters(IndexedChunk result, FileSearchFilterDto? filters)
    {
        if (filters is null)
        {
            return true;
        }

        if (filters.DocumentIds is { Count: > 0 } && !filters.DocumentIds.Contains(result.DocumentId))
        {
            return false;
        }

        if (filters.TenantId.HasValue && result.Metadata.TryGetValue("tenantId", out var tenantId) && tenantId != filters.TenantId.Value.ToString())
        {
            return false;
        }

        if (filters.Tags is { Count: > 0 })
        {
            var tags = result.Metadata.TryGetValue("tags", out var tagString)
                ? tagString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>();

            if (!filters.Tags.Any(tag => tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        if (filters.Categories is { Count: > 0 })
        {
            var categories = result.Metadata.TryGetValue("categories", out var categoriesString)
                ? categoriesString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>();

            if (!filters.Categories.Any(category => categories.Contains(category, StringComparer.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        if (filters.ContentTypes is { Count: > 0 })
        {
            var contentType = result.Metadata.TryGetValue("contentType", out var contentTypeValue)
                ? contentTypeValue
                : string.Empty;

            if (!filters.ContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (filters.Sources is { Count: > 0 })
        {
            var source = result.Metadata.TryGetValue("source", out var sourceValue)
                ? sourceValue
                : string.Empty;

            if (!filters.Sources.Contains(source, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static double CalculateScore(string query, float[]? queryEmbedding, IndexedChunk result)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(result.Content))
        {
            return queryEmbedding is { Length: > 0 } && result.Embedding is { Length: > 0 }
                ? CosineSimilarity(queryEmbedding, result.Embedding)
                : 0.1;
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matches = terms.Count(term => result.Content.Contains(term, StringComparison.OrdinalIgnoreCase));
        var lexicalScore = 0.4 + (matches / (double)Math.Max(terms.Length, 1)) * 0.6;

        if (queryEmbedding is not { Length: > 0 } || result.Embedding is not { Length: > 0 })
        {
            return Math.Round(lexicalScore, 2);
        }

        var vectorScore = CosineSimilarity(queryEmbedding, result.Embedding);
        return Math.Round((lexicalScore * 0.4) + (vectorScore * 0.6), 4);
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        if (length == 0)
        {
            return 0;
        }

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (var index = 0; index < length; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }

        if (leftNorm <= 0 || rightNorm <= 0)
        {
            return 0;
        }

        return (dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm)) + 1d) / 2d;
    }

    private Dictionary<string, IndexedChunk> Load()
    {
        if (!File.Exists(_indexFilePath))
        {
            return new Dictionary<string, IndexedChunk>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(_indexFilePath);
            var values = JsonSerializer.Deserialize<List<IndexedChunk>>(json, SerializerOptions) ?? new List<IndexedChunk>();
            return values.ToDictionary(item => item.ChunkId, IndexedChunk.Clone, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, IndexedChunk>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Persist()
    {
        var tempFilePath = _indexFilePath + ".tmp";
        var payload = JsonSerializer.Serialize(_index.Values.OrderBy(item => item.DocumentId).ThenBy(item => item.ChunkId).ToList(), SerializerOptions);
        File.WriteAllText(tempFilePath, payload);
        File.Move(tempFilePath, _indexFilePath, overwrite: true);
    }

    private static string ResolveBasePath(string configuredPath, string contentRootPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }

    private static DocumentChunkIndexDto CloneChunk(DocumentChunkIndexDto chunk)
    {
        return new DocumentChunkIndexDto
        {
            ChunkId = chunk.ChunkId,
            DocumentId = chunk.DocumentId,
            Content = chunk.Content,
            Embedding = chunk.Embedding?.ToArray(),
            PageNumber = chunk.PageNumber,
            Section = chunk.Section,
            Metadata = new Dictionary<string, string>(chunk.Metadata)
        };
    }

    private sealed class IndexedChunk
    {
        public string ChunkId { get; set; } = string.Empty;
        public Guid DocumentId { get; set; }
        public string Content { get; set; } = string.Empty;
        public double Score { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public float[]? Embedding { get; set; }

        public DocumentChunkIndexDto ToDocumentChunk()
        {
            return new DocumentChunkIndexDto
            {
                ChunkId = ChunkId,
                DocumentId = DocumentId,
                Content = Content,
                Embedding = Embedding?.ToArray(),
                PageNumber = GetPageNumber(),
                Section = Metadata.TryGetValue("section", out var section) ? section : null,
                Metadata = new Dictionary<string, string>(Metadata)
            };
        }

        public int GetChunkIndex()
        {
            return Metadata.TryGetValue("chunkIndex", out var rawChunkIndex) && int.TryParse(rawChunkIndex, out var chunkIndex)
                ? chunkIndex
                : 0;
        }

        private int GetPageNumber()
        {
            return Metadata.TryGetValue("startPage", out var rawStartPage) && int.TryParse(rawStartPage, out var startPage)
                ? startPage
                : Metadata.TryGetValue("page", out var rawPage) && int.TryParse(rawPage, out var page)
                ? page
                : 0;
        }

        public static IndexedChunk From(DocumentChunkIndexDto chunk, double score = 0.95)
        {
            return new IndexedChunk
            {
                ChunkId = chunk.ChunkId,
                DocumentId = chunk.DocumentId,
                Content = chunk.Content,
                Score = score,
                Metadata = new Dictionary<string, string>(chunk.Metadata),
                Embedding = chunk.Embedding?.ToArray()
            };
        }

        public static IndexedChunk Clone(IndexedChunk source)
        {
            return new IndexedChunk
            {
                ChunkId = source.ChunkId,
                DocumentId = source.DocumentId,
                Content = source.Content,
                Score = source.Score,
                Metadata = new Dictionary<string, string>(source.Metadata),
                Embedding = source.Embedding?.ToArray()
            };
        }
    }
}