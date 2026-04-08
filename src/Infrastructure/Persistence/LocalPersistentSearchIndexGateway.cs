using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence;

public sealed class LocalPersistentSearchIndexGateway : ISearchIndexGateway
{
    private readonly LocalPersistentChunkIndexOperation _chunkIndexOperation;
    private readonly LocalPersistentChunkReadOperation _chunkReadOperation;
    private readonly LocalPersistentHybridSearchOperation _hybridSearchOperation;
    private readonly LocalPersistentDocumentDeleteOperation _documentDeleteOperation;

    public LocalPersistentSearchIndexGateway(IDocumentCatalog documentCatalog, IOptions<LocalPersistenceOptions> options, Microsoft.Extensions.Hosting.IHostEnvironment environment)
        : this(
            CreateOperations(documentCatalog, options, environment))
    {
    }

    internal LocalPersistentSearchIndexGateway(
        (LocalPersistentChunkIndexOperation ChunkIndexOperation,
        LocalPersistentChunkReadOperation ChunkReadOperation,
        LocalPersistentHybridSearchOperation HybridSearchOperation,
        LocalPersistentDocumentDeleteOperation DocumentDeleteOperation) operations)
    {
        _chunkIndexOperation = operations.ChunkIndexOperation;
        _chunkReadOperation = operations.ChunkReadOperation;
        _hybridSearchOperation = operations.HybridSearchOperation;
        _documentDeleteOperation = operations.DocumentDeleteOperation;
    }

    public Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct)
    {
        return _chunkIndexOperation.ExecuteAsync(chunks, ct);
    }

    public Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct)
    {
        return _chunkReadOperation.ExecuteAsync(documentId, ct);
    }

    public Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct)
    {
        return _hybridSearchOperation.ExecuteAsync(query, queryEmbedding, topK, filters, ct);
    }

    public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
    {
        return _documentDeleteOperation.ExecuteAsync(documentId, ct);
    }

    private static (LocalPersistentChunkIndexOperation ChunkIndexOperation,
        LocalPersistentChunkReadOperation ChunkReadOperation,
        LocalPersistentHybridSearchOperation HybridSearchOperation,
        LocalPersistentDocumentDeleteOperation DocumentDeleteOperation) CreateOperations(
        IDocumentCatalog documentCatalog,
        IOptions<LocalPersistenceOptions> options,
        Microsoft.Extensions.Hosting.IHostEnvironment environment)
    {
        var storage = new LocalPersistentSearchStorage(options, environment);
        var fallbackSource = new LocalPersistentSearchFallbackSource(documentCatalog);
        var filter = new LocalPersistentSearchFilter();
        var scoreCalculator = new LocalPersistentSearchScoreCalculator();

        return (
            new LocalPersistentChunkIndexOperation(storage),
            new LocalPersistentChunkReadOperation(storage, fallbackSource),
            new LocalPersistentHybridSearchOperation(storage, fallbackSource, filter, scoreCalculator),
            new LocalPersistentDocumentDeleteOperation(storage));
    }
}