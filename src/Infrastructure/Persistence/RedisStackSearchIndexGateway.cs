using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Observability;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using AppCfg = Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure.Persistence;

public sealed class RedisStackSearchIndexGateway : ISearchIndexGateway, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AppCfg.VectorStoreOptions _vectorOptions;
    private readonly RedisSettings _redisSettings;
    private readonly ILogger<RedisStackSearchIndexGateway> _logger;
    private readonly ISearchIndexGateway _fallbackGateway;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    private ConnectionMultiplexer? _connection;
    private IDatabase? _database;
    private bool _redisSearchUnavailable;
    private bool _indexEnsured;

    public RedisStackSearchIndexGateway(
        IOptions<AppCfg.VectorStoreOptions> vectorOptions,
        IOptions<RedisSettings> redisSettings,
        ILogger<RedisStackSearchIndexGateway> logger,
        ISearchIndexGateway fallbackGateway)
    {
        _vectorOptions = vectorOptions.Value;
        _redisSettings = redisSettings.Value;
        _logger = logger;
        _fallbackGateway = fallbackGateway;
    }

    public async Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        var database = await GetDatabaseAsync(ct);
        if (database is null)
        {
            await _fallbackGateway.IndexDocumentChunksAsync(chunks, ct);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await EnsureIndexAsync(database, ct);

            var tasks = chunks.Select(chunk => UpsertChunkAsync(database, chunk)).ToArray();
            await Task.WhenAll(tasks);
            await _fallbackGateway.IndexDocumentChunksAsync(chunks, ct);

            ChatbotTelemetry.VectorStoreLatencyMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                new KeyValuePair<string, object?>("vector.operation", "upsert"),
                new KeyValuePair<string, object?>("vector.provider", "redisstack"));
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            await _fallbackGateway.IndexDocumentChunksAsync(chunks, ct);
        }
    }

    public async Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct)
    {
        var database = await GetDatabaseAsync(ct);
        if (database is null)
        {
            return await _fallbackGateway.GetDocumentChunksAsync(documentId, ct);
        }

        try
        {
            await EnsureIndexAsync(database, ct);
            var filter = $"@documentId:{{{EscapeTag(documentId.ToString())}}}";
            var response = await database.ExecuteAsync("FT.SEARCH", _vectorOptions.IndexName, filter, "NOCONTENT", "LIMIT", 0, 10000);
            var documentKeys = ParseDocumentKeys(response);

            if (documentKeys.Count == 0)
            {
                return await _fallbackGateway.GetDocumentChunksAsync(documentId, ct);
            }

            var readTasks = documentKeys.Select(key => database.HashGetAllAsync(key)).ToArray();
            var hashes = await Task.WhenAll(readTasks);

            var chunks = hashes
                .Select(ToChunk)
                .Where(chunk => chunk is not null)
                .Cast<DocumentChunkIndexDto>()
                .OrderBy(chunk => ParseChunkIndex(chunk.Metadata))
                .ThenBy(chunk => chunk.ChunkId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return chunks;
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            return await _fallbackGateway.GetDocumentChunksAsync(documentId, ct);
        }
    }

    public async Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        var database = await GetDatabaseAsync(ct);
        if (database is null)
        {
            return await _fallbackGateway.HybridSearchAsync(query, queryEmbedding, topK, filters, ct);
        }

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await EnsureIndexAsync(database, ct);

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
            MarkUnavailable(ex);
            return await _fallbackGateway.HybridSearchAsync(query, queryEmbedding, topK, filters, ct);
        }
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
    {
        var database = await GetDatabaseAsync(ct);
        if (database is null)
        {
            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
            return;
        }

        try
        {
            await EnsureIndexAsync(database, ct);
            var filter = $"@documentId:{{{EscapeTag(documentId.ToString())}}}";
            var response = await database.ExecuteAsync("FT.SEARCH", _vectorOptions.IndexName, filter, "NOCONTENT", "LIMIT", 0, 10000);
            var documentKeys = ParseDocumentKeys(response);

            if (documentKeys.Count > 0)
            {
                var deleteTasks = documentKeys.Select(key => database.KeyDeleteAsync(key)).ToArray();
                await Task.WhenAll(deleteTasks);
            }

            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _initializationLock.Dispose();
    }

    private async Task UpsertChunkAsync(IDatabase database, DocumentChunkIndexDto chunk)
    {
        var metadata = new Dictionary<string, string>(chunk.Metadata, StringComparer.OrdinalIgnoreCase);
        var tenantId = ReadMetadata(metadata, "tenantId");
        var key = BuildKey(tenantId, chunk.ChunkId);
        var entries = new HashEntry[]
        {
            new("chunkId", chunk.ChunkId),
            new("tenantId", tenantId),
            new("documentId", chunk.DocumentId.ToString()),
            new("chunkIndex", ReadMetadata(metadata, "chunkIndex", ResolveChunkIndex(chunk.ChunkId))),
            new("content", chunk.Content),
            new("title", ReadMetadata(metadata, "title")),
            new("sourceName", ReadMetadata(metadata, "sourceName", ReadMetadata(metadata, "originalFileName"))),
            new("sourceType", ReadMetadata(metadata, "sourceType", ReadMetadata(metadata, "source"))),
            new("contentType", ReadMetadata(metadata, "contentType")),
            new("sectionTitle", chunk.Section ?? ReadMetadata(metadata, "section")),
            new("pageNumber", chunk.PageNumber.ToString(CultureInfo.InvariantCulture)),
            new("endPageNumber", ReadMetadata(metadata, "endPage", chunk.PageNumber.ToString(CultureInfo.InvariantCulture))),
            new("tags", ToTagValue(ReadMetadata(metadata, "tags"))),
            new("categories", ToTagValue(ReadMetadata(metadata, "categories"))),
            new("accessPolicy", ReadMetadata(metadata, "accessPolicy")),
            new("contentHash", ReadMetadata(metadata, "contentHash")),
            new("metadata", JsonSerializer.Serialize(metadata, SerializerOptions)),
            new("vector", EncodeVector(chunk.Embedding ?? Array.Empty<float>()))
        };

        await database.HashSetAsync(key, entries);
    }

    private async Task<IDatabase?> GetDatabaseAsync(CancellationToken ct)
    {
        if (!string.Equals(_vectorOptions.Provider, "redisstack", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (_redisSearchUnavailable)
        {
            return null;
        }

        if (_database is not null)
        {
            return _database;
        }

        await _initializationLock.WaitAsync(ct);
        try
        {
            if (_database is not null)
            {
                return _database;
            }

            var connectionString = ResolveConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return null;
            }

            _connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
            _database = _connection.GetDatabase();
            return _database;
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            return null;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task EnsureIndexAsync(IDatabase database, CancellationToken ct)
    {
        if (_indexEnsured)
        {
            return;
        }

        await _initializationLock.WaitAsync(ct);
        try
        {
            if (_indexEnsured)
            {
                return;
            }

            try
            {
                await database.ExecuteAsync("FT.INFO", _vectorOptions.IndexName);
                _indexEnsured = true;
                return;
            }
            catch
            {
            }

            var createArguments = new List<object>
            {
                _vectorOptions.IndexName,
                "ON", "HASH",
                "PREFIX", 1, _vectorOptions.KeyPrefix,
                "SCHEMA",
                "chunkId", "TAG",
                "tenantId", "TAG",
                "documentId", "TAG",
                "chunkIndex", "NUMERIC",
                "content", "TEXT",
                "title", "TEXT",
                "sourceName", "TEXT",
                "sourceType", "TAG",
                "contentType", "TAG",
                "sectionTitle", "TEXT",
                "pageNumber", "NUMERIC",
                "endPageNumber", "NUMERIC",
                "tags", "TAG", "SEPARATOR", "|",
                "categories", "TAG", "SEPARATOR", "|",
                "accessPolicy", "TAG",
                "contentHash", "TAG",
                "metadata", "TEXT",
                "vector", "VECTOR", "HNSW", 6,
                "TYPE", "FLOAT32",
                "DIM", _vectorOptions.Dimensions,
                "DISTANCE_METRIC", "COSINE"
            };

            await database.ExecuteAsync("FT.CREATE", createArguments.ToArray());
            _indexEnsured = true;
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<Dictionary<string, RedisSearchResultAccumulator>> SearchByVectorAsync(IDatabase database, float[] queryEmbedding, int candidateCount, FileSearchFilterDto? filters)
    {
        var searchQuery = $"{BuildFilterQuery(filters)}=>[KNN {candidateCount} @vector $BLOB AS vector_score]";
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

        return ParseSearchResponse(response, isVectorResult: true);
    }

    private async Task<Dictionary<string, RedisSearchResultAccumulator>> SearchLexicalAsync(IDatabase database, string query, int candidateCount, FileSearchFilterDto? filters)
    {
        var escapedTerms = EscapeTextQuery(query);
        var lexicalQuery = string.IsNullOrWhiteSpace(escapedTerms)
            ? BuildFilterQuery(filters)
            : $"{BuildFilterQuery(filters)} {escapedTerms}".Trim();

        var response = await database.ExecuteAsync(
            "FT.SEARCH",
            _vectorOptions.IndexName,
            lexicalQuery,
            "WITHSCORES",
            "RETURN", 12,
            "chunkId", "documentId", "content", "title", "sourceName", "sourceType", "contentType", "sectionTitle", "pageNumber", "endPageNumber", "metadata", "accessPolicy",
            "LIMIT", 0, candidateCount);

        return ParseSearchResponse(response, isVectorResult: false);
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

    private Dictionary<string, RedisSearchResultAccumulator> ParseSearchResponse(RedisResult response, bool isVectorResult)
    {
        var items = new Dictionary<string, RedisSearchResultAccumulator>(StringComparer.OrdinalIgnoreCase);
        if (response.IsNull)
        {
            return items;
        }

        var values = (RedisResult[])response!;
        if (values.Length <= 1)
        {
            return items;
        }

        var step = isVectorResult ? 2 : 3;
        for (var index = 1; index < values.Length; index += step)
        {
            var documentKey = values[index].ToString();
            RedisResult[] fieldEntries;
            double textualScore = 0;

            if (isVectorResult)
            {
                fieldEntries = (RedisResult[])values[index + 1]!;
            }
            else
            {
                textualScore = double.TryParse(values[index + 1].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedScore)
                    ? parsedScore
                    : 0;
                fieldEntries = (RedisResult[])values[index + 2]!;
            }

            var fields = ToFieldDictionary(fieldEntries);
            var chunkId = ReadField(fields, "chunkId");
            if (string.IsNullOrWhiteSpace(chunkId))
            {
                chunkId = ExtractChunkIdFromKey(documentKey);
            }

            if (!Guid.TryParse(ReadField(fields, "documentId"), out var documentId))
            {
                continue;
            }

            var metadataJson = ReadField(fields, "metadata", "{}");
            var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson, SerializerOptions)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!metadata.ContainsKey("title"))
            {
                metadata["title"] = ReadField(fields, "title");
            }

            if (!metadata.ContainsKey("documentTitle"))
            {
                metadata["documentTitle"] = ReadField(fields, "title");
            }

            if (!metadata.ContainsKey("section"))
            {
                metadata["section"] = ReadField(fields, "sectionTitle");
            }

            if (!metadata.ContainsKey("page"))
            {
                metadata["page"] = ReadField(fields, "pageNumber");
            }

            if (!metadata.ContainsKey("endPage"))
            {
                metadata["endPage"] = ReadField(fields, "endPageNumber");
            }

            var result = new RedisSearchResultAccumulator
            {
                ChunkId = chunkId,
                DocumentId = documentId,
                Content = ReadField(fields, "content"),
                Metadata = metadata,
                VectorScore = isVectorResult ? NormalizeVectorScore(ReadField(fields, "vector_score")) : 0,
                LexicalScore = isVectorResult ? 0 : NormalizeLexicalScore(textualScore)
            };

            items[chunkId] = result;
        }

        return items;
    }

    private static List<RedisKey> ParseDocumentKeys(RedisResult response)
    {
        var keys = new List<RedisKey>();
        if (response.IsNull)
        {
            return keys;
        }

        var values = (RedisResult[])response!;
        for (var index = 1; index < values.Length; index++)
        {
            keys.Add(values[index].ToString());
        }

        return keys;
    }

    private static Dictionary<string, string> ToFieldDictionary(RedisResult[] fieldEntries)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index + 1 < fieldEntries.Length; index += 2)
        {
            fields[fieldEntries[index].ToString()] = fieldEntries[index + 1].ToString();
        }

        return fields;
    }

    private string BuildFilterQuery(FileSearchFilterDto? filters)
    {
        var clauses = new List<string>();

        if (filters?.TenantId is Guid tenantId && tenantId != Guid.Empty)
        {
            clauses.Add($"@tenantId:{{{EscapeTag(tenantId.ToString())}}}");
        }

        if (filters?.DocumentIds is { Count: > 0 })
        {
            clauses.Add(BuildTagClause("documentId", filters.DocumentIds.Select(id => id.ToString())));
        }

        if (filters?.Tags is { Count: > 0 })
        {
            clauses.Add(BuildTagClause("tags", filters.Tags));
        }

        if (filters?.Categories is { Count: > 0 })
        {
            clauses.Add(BuildTagClause("categories", filters.Categories));
        }

        if (filters?.ContentTypes is { Count: > 0 })
        {
            clauses.Add(BuildTagClause("contentType", filters.ContentTypes));
        }

        if (filters?.Sources is { Count: > 0 })
        {
            clauses.Add(BuildTagClause("sourceType", filters.Sources));
        }

        return clauses.Count == 0 ? "*" : string.Join(' ', clauses);
    }

    private static string BuildTagClause(string fieldName, IEnumerable<string> values)
    {
        var normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(EscapeTag)
            .ToArray();

        return normalized.Length == 0
            ? string.Empty
            : $"@{fieldName}:{{{string.Join('|', normalized)}}}";
    }

    private static string EscapeTag(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is '\\' or '-' or '|' or '{' or '}' or '[' or ']' or '(' or ')' or '"' or ':' or ';' or ',' or '.' or '<' or '>' or '~' or '!' or '@' or '#')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string EscapeTextQuery(string query)
    {
        var tokens = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', tokens.Select(token => token.Replace("-", "\\-", StringComparison.Ordinal)));
    }

    private string ResolveConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(_vectorOptions.ConnectionString))
        {
            return _vectorOptions.ConnectionString;
        }

        if (string.IsNullOrWhiteSpace(_redisSettings.Server) || _redisSettings.Port <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append(_redisSettings.Server);
        builder.Append(':');
        builder.Append(_redisSettings.Port);
        builder.Append(",abortConnect=false");

        if (!string.IsNullOrWhiteSpace(_redisSettings.Password))
        {
            builder.Append(",password=");
            builder.Append(_redisSettings.Password);
        }

        return builder.ToString();
    }

    private string BuildKey(string tenantId, string chunkId)
    {
        return $"{_vectorOptions.KeyPrefix}{tenantId}:{chunkId}";
    }

    private static string ExtractChunkIdFromKey(string key)
    {
        var lastSeparator = key.LastIndexOf(':');
        return lastSeparator >= 0 && lastSeparator + 1 < key.Length
            ? key[(lastSeparator + 1)..]
            : key;
    }

    private static string ToTagValue(string csv)
    {
        return string.Join('|', csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string ResolveChunkIndex(string chunkId)
    {
        var suffix = chunkId.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        return int.TryParse(suffix, out var parsed) ? parsed.ToString(CultureInfo.InvariantCulture) : "0";
    }

    private static double NormalizeVectorScore(string rawDistance)
    {
        if (!double.TryParse(rawDistance, NumberStyles.Float, CultureInfo.InvariantCulture, out var distance))
        {
            return 0;
        }

        return Math.Round(1d / (1d + Math.Max(0d, distance)), 4);
    }

    private static double NormalizeLexicalScore(double score)
    {
        return Math.Round(Math.Max(0d, Math.Min(1d, score)), 4);
    }

    private static byte[] EncodeVector(float[] vector)
    {
        var payload = new byte[vector.Length * sizeof(float)];
        for (var index = 0; index < vector.Length; index++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(index * sizeof(float), sizeof(float)), vector[index]);
        }

        return payload;
    }

    private void MarkUnavailable(Exception ex)
    {
        if (_redisSearchUnavailable)
        {
            return;
        }

        _redisSearchUnavailable = true;
        _logger.LogWarning(ex, "Redis Stack/RediSearch indisponivel; fallback local persistente sera usado.");
        _connection?.Dispose();
        _connection = null;
        _database = null;
    }

    private static string ReadMetadata(Dictionary<string, string> metadata, string key, string fallback = "")
    {
        return metadata.TryGetValue(key, out var value) ? value ?? fallback : fallback;
    }

    private static string ReadField(Dictionary<string, string> fields, string key, string fallback = "")
    {
        return fields.TryGetValue(key, out var value) ? value ?? fallback : fallback;
    }

    private static DocumentChunkIndexDto? ToChunk(HashEntry[] entries)
    {
        if (entries.Length == 0)
        {
            return null;
        }

        var fields = entries.ToDictionary(entry => entry.Name.ToString(), entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        if (!Guid.TryParse(fields.TryGetValue("documentId", out var rawDocumentId) ? rawDocumentId.ToString() : null, out var documentId))
        {
            return null;
        }

        var metadata = fields.TryGetValue("metadata", out var rawMetadata)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(rawMetadata.ToString(), SerializerOptions)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var pageNumber = fields.TryGetValue("pageNumber", out var rawPageNumber) && int.TryParse(rawPageNumber.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPageNumber)
            ? parsedPageNumber
            : 0;

        var embedding = fields.TryGetValue("vector", out var rawVector) && !rawVector.IsNull
            ? DecodeVector((byte[])rawVector!)
            : null;

        return new DocumentChunkIndexDto
        {
            ChunkId = fields.TryGetValue("chunkId", out var rawChunkId) ? rawChunkId.ToString() : string.Empty,
            DocumentId = documentId,
            Content = fields.TryGetValue("content", out var rawContent) ? rawContent.ToString() : string.Empty,
            Embedding = embedding,
            PageNumber = pageNumber,
            Section = fields.TryGetValue("sectionTitle", out var rawSection) ? rawSection.ToString() : null,
            Metadata = metadata
        };
    }

    private static float[]? DecodeVector(byte[] payload)
    {
        if (payload.Length == 0 || payload.Length % sizeof(float) != 0)
        {
            return null;
        }

        var vector = new float[payload.Length / sizeof(float)];
        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] = BinaryPrimitives.ReadSingleLittleEndian(payload.AsSpan(index * sizeof(float), sizeof(float)));
        }

        return vector;
    }

    private static int ParseChunkIndex(Dictionary<string, string> metadata)
    {
        return metadata.TryGetValue("chunkIndex", out var rawChunkIndex) && int.TryParse(rawChunkIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chunkIndex)
            ? chunkIndex
            : 0;
    }

}