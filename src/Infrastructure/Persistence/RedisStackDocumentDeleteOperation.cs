using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisStackDocumentDeleteOperation
{
    private readonly VectorStoreOptions _vectorOptions;
    private readonly ISearchIndexGateway _fallbackGateway;
    private readonly RedisStackDatabaseAccessor _databaseAccessor;
    private readonly RedisStackDocumentKeyParser _documentKeyParser;

    public RedisStackDocumentDeleteOperation(
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

    public async Task ExecuteAsync(Guid documentId, CancellationToken ct)
    {
        var database = await _databaseAccessor.GetDatabaseAsync(ct);
        if (database is null)
        {
            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
            return;
        }

        try
        {
            await _databaseAccessor.EnsureIndexAsync(database, ct);
            var filter = $"@documentId:{{{documentId}}}";
            var response = await database.ExecuteAsync("FT.SEARCH", _vectorOptions.IndexName, filter, "NOCONTENT", "LIMIT", 0, 10000);
            var documentKeys = _documentKeyParser.Parse(response);

            if (documentKeys.Count > 0)
            {
                var deleteTasks = documentKeys.Select(key => database.KeyDeleteAsync(key)).ToArray();
                await Task.WhenAll(deleteTasks);
            }

            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
        }
        catch (Exception ex)
        {
            _databaseAccessor.MarkUnavailable(ex);
            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
        }
    }
}