using System.Diagnostics;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Observability;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisStackHybridSearchOperation
{
    private readonly VectorStoreOptions _vectorOptions;
    private readonly ISearchIndexGateway _fallbackGateway;
    private readonly RedisStackDatabaseAccessor _databaseAccessor;
    private readonly RedisStackFilterQueryBuilder _filterQueryBuilder;

    public RedisStackHybridSearchOperation(
        VectorStoreOptions vectorOptions,
        ISearchIndexGateway fallbackGateway,
        RedisStackDatabaseAccessor databaseAccessor,
        RedisStackFilterQueryBuilder filterQueryBuilder)
    {
        _vectorOptions = vectorOptions;
        _fallbackGateway = fallbackGateway;
        _databaseAccessor = databaseAccessor;
        _filterQueryBuilder = filterQueryBuilder;
    }

    public async Task<List<SearchResultDto>> ExecuteAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        var database = await _databaseAccessor.GetDatabaseAsync(ct);
        if (database is null)
        {
            return await _fallbackGateway.HybridSearchAsync(query, queryEmbedding, topK, filters, ct);
        }

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await _databaseAccessor.EnsureIndexAsync(database, ct);

            var candidateCount = Math.Max(topK, topK * Math.Max(1, _vectorOptions.CandidateMultiplier));
            var vectorResults = queryEmbedding is { Length: > 0 }
                ? await SearchByVectorAsync(database, queryEmbedding, candidateCount, filters)
                : new Dictionary<string, RedisSearchResultAccumulator>(StringComparer.OrdinalIgnoreCase);

            var lexicalResults = !string.IsNullOrWhiteSpace(query)
                ? await SearchLexicalAsync(database, query, candidateCount, filters)
                : new Dictionary<string, RedisSearchResultAccumulator>(StringComparer.OrdinalIgnoreCase);

            var merged = MergeResults(vectorResults, lexicalResults)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.ChunkId, StringComparer.OrdinalIgnoreCase)
                .Take(topK)
                .Select(item => item.ToDto())
                .ToList();

            ChatbotTelemetry.VectorStoreLatencyMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                new KeyValuePair<string, object?>("vector.operation", "search"),
                new KeyValuePair<string, object?>("vector.provider", "redisstack"));

            return merged;
        }
        catch (Exception ex)
        {
            _databaseAccessor.MarkUnavailable(ex);
            return await _fallbackGateway.HybridSearchAsync(query, queryEmbedding, topK, filters, ct);
        }
    }

    private async Task<Dictionary<string, RedisSearchResultAccumulator>> SearchByVectorAsync(StackExchange.Redis.IDatabase database, float[] queryEmbedding, int candidateCount, FileSearchFilterDto? filters)
    {
        var searchQuery = $"{_filterQueryBuilder.Build(filters)}=>[KNN {candidateCount} @vector $BLOB AS vector_score]";
        var response = await database.ExecuteAsync(
            "FT.SEARCH",
            _vectorOptions.IndexName,
            searchQuery,
            "PARAMS", 2, "BLOB", EncodeVector(queryEmbedding),
            "SORTBY", "vector_score",
            "RETURN", 13,
            "chunkId", "documentId", "content", "title", "sourceName", "sourceType", "contentType", "sectionTitle", "pageNumber", "endPageNumber", "metadata", "accessPolicy", "vector_score",
            "LIMIT", 0, candidateCount,
            "DIALECT", 2);

        return RedisSearchResponseMapper.ParseSearchResponse(response, isVectorResult: true);
    }

    private async Task<Dictionary<string, RedisSearchResultAccumulator>> SearchLexicalAsync(StackExchange.Redis.IDatabase database, string query, int candidateCount, FileSearchFilterDto? filters)
    {
        var escapedTerms = _filterQueryBuilder.EscapeTextQuery(query);
        var lexicalQuery = string.IsNullOrWhiteSpace(escapedTerms)
            ? _filterQueryBuilder.Build(filters)
            : $"{_filterQueryBuilder.Build(filters)} {escapedTerms}".Trim();

        var response = await database.ExecuteAsync(
            "FT.SEARCH",
            _vectorOptions.IndexName,
            lexicalQuery,
            "WITHSCORES",
            "RETURN", 12,
            "chunkId", "documentId", "content", "title", "sourceName", "sourceType", "contentType", "sectionTitle", "pageNumber", "endPageNumber", "metadata", "accessPolicy",
            "LIMIT", 0, candidateCount);

        return RedisSearchResponseMapper.ParseSearchResponse(response, isVectorResult: false);
    }

    private IEnumerable<RedisSearchResultAccumulator> MergeResults(
        Dictionary<string, RedisSearchResultAccumulator> vectorResults,
        Dictionary<string, RedisSearchResultAccumulator> lexicalResults)
    {
        var allChunkIds = vectorResults.Keys.Union(lexicalResults.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var chunkId in allChunkIds)
        {
            vectorResults.TryGetValue(chunkId, out var vector);
            lexicalResults.TryGetValue(chunkId, out var lexical);

            var resolved = vector ?? lexical;
            if (resolved is null)
            {
                continue;
            }

            var vectorScore = vector?.VectorScore ?? 0;
            var lexicalScore = lexical?.LexicalScore ?? 0;
            resolved.Score = vector is not null && lexical is not null
                ? Math.Round((vectorScore * 0.65d) + (lexicalScore * 0.35d), 4)
                : Math.Round(Math.Max(vectorScore, lexicalScore), 4);

            yield return resolved;
        }
    }

    private static byte[] EncodeVector(float[] vector)
    {
        var payload = new byte[vector.Length * sizeof(float)];
        for (var index = 0; index < vector.Length; index++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(index * sizeof(float), sizeof(float)), vector[index]);
        }

        return payload;
    }
}