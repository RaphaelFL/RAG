using Chatbot.Application.Abstractions;
using NpgsqlTypes;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class PgVectorChunkReadOperation
{
    private readonly ISearchIndexGateway _fallbackGateway;
    private readonly PgVectorSqlBuilder _sqlBuilder;
    private readonly PgVectorResultMapper _resultMapper;
    private readonly PgVectorDataSourceProvider _dataSourceProvider;

    public PgVectorChunkReadOperation(
        ISearchIndexGateway fallbackGateway,
        PgVectorSqlBuilder sqlBuilder,
        PgVectorResultMapper resultMapper,
        PgVectorDataSourceProvider dataSourceProvider)
    {
        _fallbackGateway = fallbackGateway;
        _sqlBuilder = sqlBuilder;
        _resultMapper = resultMapper;
        _dataSourceProvider = dataSourceProvider;
    }

    public async Task<List<DocumentChunkIndexDto>> ExecuteAsync(Guid documentId, CancellationToken ct)
    {
        var dataSource = await _dataSourceProvider.GetDataSourceAsync(ct);
        if (dataSource is null)
        {
            return await _fallbackGateway.GetDocumentChunksAsync(documentId, ct);
        }

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = _sqlBuilder.BuildGetDocumentChunksSql();
            command.Parameters.AddWithValue("document_id", NpgsqlDbType.Uuid, documentId);

            var chunks = new List<DocumentChunkIndexDto>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                chunks.Add(_resultMapper.MapChunk(reader));
            }

            return chunks.Count > 0
                ? chunks
                : await _fallbackGateway.GetDocumentChunksAsync(documentId, ct);
        }
        catch (Exception ex)
        {
            _dataSourceProvider.MarkUnavailable(ex);
            return await _fallbackGateway.GetDocumentChunksAsync(documentId, ct);
        }
    }
}