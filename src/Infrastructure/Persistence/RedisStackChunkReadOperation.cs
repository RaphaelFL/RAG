using System.Globalization;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisStackChunkReadOperation
{
    private readonly VectorStoreOptions _vectorOptions;
    private readonly ISearchIndexGateway _fallbackGateway;
    private readonly RedisStackDatabaseAccessor _databaseAccessor;
    private readonly RedisStackDocumentKeyParser _documentKeyParser;

    public RedisStackChunkReadOperation(
        VectorStoreOptions vectorOptions,
        ISearchIndexGateway fallbackGateway,
        RedisStackDatabaseAccessor databaseAccessor,
        RedisStackDocumentKeyParser documentKeyParser)
    {
        _vectorOptions = vectorOptions;
        _fallbackGateway = fallbackGateway;
        _databaseAccessor = databaseAccessor;
        _documentKeyParser = documentKeyParser;
    }

    public async Task<List<DocumentChunkIndexDto>> ExecuteAsync(Guid documentId, CancellationToken ct)
    {
        var database = await _databaseAccessor.GetDatabaseAsync(ct);
        if (database is null)
        {
            return await _fallbackGateway.GetDocumentChunksAsync(documentId, ct);
        }

        try
        {
            await _databaseAccessor.EnsureIndexAsync(database, ct);
            var filter = $"@documentId:{{{documentId}}}";
            var response = await database.ExecuteAsync("FT.SEARCH", _vectorOptions.IndexName, filter, "NOCONTENT", "LIMIT", 0, 10000);
            var documentKeys = _documentKeyParser.Parse(response);

            if (documentKeys.Count == 0)
            {
                return await _fallbackGateway.GetDocumentChunksAsync(documentId, ct);
            }

            var readTasks = documentKeys.Select(key => database.HashGetAllAsync(key)).ToArray();
            var hashes = await Task.WhenAll(readTasks);

            var chunks = hashes
                .Select(RedisDocumentChunkMapper.ToChunk)
                .Where(chunk => chunk is not null)
                .Cast<DocumentChunkIndexDto>()
                .OrderBy(chunk => ParseChunkIndex(chunk.Metadata))
                .ThenBy(chunk => chunk.ChunkId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return chunks;
        }
        catch (Exception ex)
        {
            _databaseAccessor.MarkUnavailable(ex);
            return await _fallbackGateway.GetDocumentChunksAsync(documentId, ct);
        }
    }

    private static int ParseChunkIndex(Dictionary<string, string> metadata)
    {
        return metadata.TryGetValue("chunkIndex", out var rawChunkIndex) && int.TryParse(rawChunkIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chunkIndex)
            ? chunkIndex
            : 0;
    }
}