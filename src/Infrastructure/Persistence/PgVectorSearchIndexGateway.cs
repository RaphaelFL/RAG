using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence;

public sealed class PgVectorSearchIndexGateway : ISearchIndexGateway
{
    private readonly PgVectorChunkIndexOperation _chunkIndexOperation;
    private readonly PgVectorChunkReadOperation _chunkReadOperation;
    private readonly PgVectorHybridSearchOperation _hybridSearchOperation;
    private readonly PgVectorDocumentDeleteOperation _documentDeleteOperation;

    public PgVectorSearchIndexGateway(
        IOptions<VectorStoreOptions> options,
        ILogger<PgVectorSearchIndexGateway> logger,
        ISearchIndexGateway fallbackGateway)
        : this(options.Value, logger, fallbackGateway)
    {
    }

    internal PgVectorSearchIndexGateway(
        VectorStoreOptions options,
        ILogger<PgVectorSearchIndexGateway> logger,
        ISearchIndexGateway fallbackGateway)
        : this(CreateOperations(options, logger, fallbackGateway))
    {
    }

    private PgVectorSearchIndexGateway(
        (PgVectorChunkIndexOperation ChunkIndexOperation,
        PgVectorChunkReadOperation ChunkReadOperation,
        PgVectorHybridSearchOperation HybridSearchOperation,
        PgVectorDocumentDeleteOperation DocumentDeleteOperation) operations)
        : this(
            operations.ChunkIndexOperation,
            operations.ChunkReadOperation,
            operations.HybridSearchOperation,
            operations.DocumentDeleteOperation)
    {
    }

    internal PgVectorSearchIndexGateway(
        PgVectorChunkIndexOperation chunkIndexOperation,
        PgVectorChunkReadOperation chunkReadOperation,
        PgVectorHybridSearchOperation hybridSearchOperation,
        PgVectorDocumentDeleteOperation documentDeleteOperation)
    {
        _chunkIndexOperation = chunkIndexOperation;
        _chunkReadOperation = chunkReadOperation;
        _hybridSearchOperation = hybridSearchOperation;
        _documentDeleteOperation = documentDeleteOperation;
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

    private static (PgVectorChunkIndexOperation ChunkIndexOperation,
        PgVectorChunkReadOperation ChunkReadOperation,
        PgVectorHybridSearchOperation HybridSearchOperation,
        PgVectorDocumentDeleteOperation DocumentDeleteOperation) CreateOperations(
        VectorStoreOptions options,
        ILogger<PgVectorSearchIndexGateway> logger,
        ISearchIndexGateway fallbackGateway)
    {
        var sqlBuilder = new PgVectorSqlBuilder(options);
        var parameterBinder = new PgVectorCommandParameterBinder(options);
        var resultMapper = new PgVectorResultMapper();
        var dataSourceProvider = new PgVectorDataSourceProvider(options, logger, sqlBuilder);
        return (
            new PgVectorChunkIndexOperation(fallbackGateway, sqlBuilder, parameterBinder, dataSourceProvider),
            new PgVectorChunkReadOperation(fallbackGateway, sqlBuilder, resultMapper, dataSourceProvider),
            new PgVectorHybridSearchOperation(fallbackGateway, sqlBuilder, parameterBinder, resultMapper, dataSourceProvider),
            new PgVectorDocumentDeleteOperation(fallbackGateway, sqlBuilder, dataSourceProvider));
    }
}