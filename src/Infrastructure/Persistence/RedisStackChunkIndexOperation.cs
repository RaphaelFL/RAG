using System.Diagnostics;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Observability;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisStackChunkIndexOperation
{
    private readonly VectorStoreOptions _vectorOptions;
    private readonly ISearchIndexGateway _fallbackGateway;
    private readonly RedisStackDatabaseAccessor _databaseAccessor;
    private readonly RedisStackChunkHashEntryFactory _hashEntryFactory;

    public RedisStackChunkIndexOperation(
        VectorStoreOptions vectorOptions,
        ISearchIndexGateway fallbackGateway,
        RedisStackDatabaseAccessor databaseAccessor,
        RedisStackChunkHashEntryFactory hashEntryFactory)
    {
        _vectorOptions = vectorOptions;
        _fallbackGateway = fallbackGateway;
        _databaseAccessor = databaseAccessor;
        _hashEntryFactory = hashEntryFactory;
    }

    public async Task ExecuteAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        var database = await _databaseAccessor.GetDatabaseAsync(ct);
        if (database is null)
        {
            await _fallbackGateway.IndexDocumentChunksAsync(chunks, ct);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await _databaseAccessor.EnsureIndexAsync(database, ct);

            var tasks = chunks.Select(chunk => UpsertChunkAsync(database, chunk)).ToArray();
            await Task.WhenAll(tasks);
            await _fallbackGateway.IndexDocumentChunksAsync(chunks, ct);

            ChatbotTelemetry.VectorStoreLatencyMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                new KeyValuePair<string, object?>("vector.operation", "upsert"),
                new KeyValuePair<string, object?>("vector.provider", "redisstack"));
        }
        catch (Exception ex)
        {
            _databaseAccessor.MarkUnavailable(ex);
            await _fallbackGateway.IndexDocumentChunksAsync(chunks, ct);
        }
    }

    private async Task UpsertChunkAsync(StackExchange.Redis.IDatabase database, DocumentChunkIndexDto chunk)
    {
        var tenantId = _hashEntryFactory.ResolveTenantId(chunk);
        var key = _hashEntryFactory.BuildKey(tenantId, chunk.ChunkId);
        var entries = _hashEntryFactory.Build(chunk);
        await database.HashSetAsync(key, entries);
    }
}