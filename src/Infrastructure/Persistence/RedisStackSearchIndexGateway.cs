using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AppCfg = Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure.Persistence;

public sealed class RedisStackSearchIndexGateway : ISearchIndexGateway, IDisposable
{
    private readonly RedisStackDatabaseAccessor _databaseAccessor;
    private readonly RedisStackChunkIndexOperation _chunkIndexOperation;
    private readonly RedisStackChunkReadOperation _chunkReadOperation;
    private readonly RedisStackHybridSearchOperation _hybridSearchOperation;
    private readonly RedisStackDocumentDeleteOperation _documentDeleteOperation;

    public RedisStackSearchIndexGateway(
        IOptions<AppCfg.VectorStoreOptions> vectorOptions,
        IOptions<RedisSettings> redisSettings,
        ILogger<RedisStackSearchIndexGateway> logger,
        ISearchIndexGateway fallbackGateway)
        : this(vectorOptions, redisSettings, logger, fallbackGateway,
            new RedisConnectionProvider(vectorOptions, redisSettings),
            new RedisIndexEnsurer(vectorOptions))
    {
    }

    // Internal constructor for dependency injection/testing
    internal RedisStackSearchIndexGateway(
        IOptions<AppCfg.VectorStoreOptions> vectorOptions,
        IOptions<RedisSettings> redisSettings,
        ILogger<RedisStackSearchIndexGateway> logger,
        ISearchIndexGateway fallbackGateway,
        IRedisConnectionProvider connectionProvider,
        IRedisIndexEnsurer indexEnsurer)
    {
        var options = vectorOptions.Value;
        _databaseAccessor = new RedisStackDatabaseAccessor(options, logger, connectionProvider, indexEnsurer);
        var documentKeyParser = new RedisStackDocumentKeyParser();
        var hashEntryFactory = new RedisStackChunkHashEntryFactory(options);
        var filterQueryBuilder = new RedisStackFilterQueryBuilder();
        _chunkIndexOperation = new RedisStackChunkIndexOperation(options, fallbackGateway, _databaseAccessor, hashEntryFactory);
        _chunkReadOperation = new RedisStackChunkReadOperation(options, fallbackGateway, _databaseAccessor, documentKeyParser);
        _hybridSearchOperation = new RedisStackHybridSearchOperation(options, fallbackGateway, _databaseAccessor, filterQueryBuilder);
        _documentDeleteOperation = new RedisStackDocumentDeleteOperation(options, fallbackGateway, _databaseAccessor, documentKeyParser);
    }

    public async Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
    {
        await _chunkIndexOperation.ExecuteAsync(chunks, ct);
    }

    public async Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct)
    {
        return await _chunkReadOperation.ExecuteAsync(documentId, ct);
    }

    public async Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        return await _hybridSearchOperation.ExecuteAsync(query, queryEmbedding, topK, filters, ct);
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
    {
        await _documentDeleteOperation.ExecuteAsync(documentId, ct);
    }

    public void Dispose()
    {
        _databaseAccessor.Dispose();
    }

}