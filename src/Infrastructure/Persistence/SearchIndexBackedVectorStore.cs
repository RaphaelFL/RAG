using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Pgvector.Npgsql;

namespace Chatbot.Infrastructure.Persistence;

public sealed class SearchIndexBackedVectorStore : IVectorStore
{
    private readonly ISearchIndexGateway _searchIndexGateway;

    public SearchIndexBackedVectorStore(ISearchIndexGateway searchIndexGateway)
    {
        _searchIndexGateway = searchIndexGateway;
    }

    public async Task UpsertAsync(VectorUpsertRequest request, CancellationToken ct)
    {
        var chunks = request.Chunks.Select((chunk, index) => new DocumentChunkIndexDto
        {
            ChunkId = chunk.ChunkId,
            DocumentId = request.DocumentId,
            Content = chunk.Text,
            Embedding = chunk.Vector,
            PageNumber = ParseInt(ReadMetadata(chunk.Metadata, "startPage"), 0),
            Section = ReadMetadata(chunk.Metadata, "section"),
            Metadata = new Dictionary<string, string>(chunk.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["tenantId"] = request.TenantId.ToString(),
                ["chunkIndex"] = index.ToString(),
                ["vectorDimensions"] = chunk.Vector.Length.ToString()
            }
        }).ToList();

        await _searchIndexGateway.IndexDocumentChunksAsync(chunks, ct);
    }

    public async Task<VectorSearchResult> SearchAsync(VectorSearchRequest request, CancellationToken ct)
    {
        var filters = new FileSearchFilterDto
        {
            TenantId = request.TenantId,
            Categories = request.Filters.TryGetValue("categories", out var categories) ? categories.ToList() : null,
            Tags = request.Filters.TryGetValue("tags", out var tags) ? tags.ToList() : null,
            Sources = request.Filters.TryGetValue("sources", out var sources) ? sources.ToList() : null,
            ContentTypes = request.Filters.TryGetValue("contentTypes", out var contentTypes) ? contentTypes.ToList() : null,
            DocumentIds = request.Filters.TryGetValue("documentIds", out var documentIds)
                ? documentIds.Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty).Where(value => value != Guid.Empty).ToList()
                : null
        };

        var results = await _searchIndexGateway.HybridSearchAsync(request.QueryText, request.QueryVector, request.TopK, filters, ct);
        var filtered = results
            .Where(result => result.Score >= request.ScoreThreshold)
            .Select(result => new RetrievedChunk
            {
                ChunkId = result.ChunkId,
                DocumentId = result.DocumentId,
                Score = result.Score,
                Text = result.Content,
                Metadata = result.Metadata
            })
            .ToArray();

        return new VectorSearchResult
        {
            Chunks = filtered,
            Strategy = request.QueryVector is { Length: > 0 } ? "pgvector-hybrid" : "pgvector-lexical"
        };
    }

    public Task DeleteDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct)
    {
        return _searchIndexGateway.DeleteDocumentAsync(documentId, ct);
    }

    private static int ParseInt(string? rawValue, int fallback)
    {
        return int.TryParse(rawValue, out var parsed) ? parsed : fallback;
    }

    private static string ReadMetadata(Dictionary<string, string> metadata, string key, string fallback = "")
    {
        return metadata.TryGetValue(key, out var value) ? value ?? fallback : fallback;
    }
}
