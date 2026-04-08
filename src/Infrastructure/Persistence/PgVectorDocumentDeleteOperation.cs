using Chatbot.Application.Abstractions;
using NpgsqlTypes;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class PgVectorDocumentDeleteOperation
{
    private readonly ISearchIndexGateway _fallbackGateway;
    private readonly PgVectorSqlBuilder _sqlBuilder;
    private readonly PgVectorDataSourceProvider _dataSourceProvider;

    public PgVectorDocumentDeleteOperation(
        ISearchIndexGateway fallbackGateway,
        PgVectorSqlBuilder sqlBuilder,
        PgVectorDataSourceProvider dataSourceProvider)
    {
        _fallbackGateway = fallbackGateway;
        _sqlBuilder = sqlBuilder;
        _dataSourceProvider = dataSourceProvider;
    }

    public async Task ExecuteAsync(Guid documentId, CancellationToken ct)
    {
        var dataSource = await _dataSourceProvider.GetDataSourceAsync(ct);
        if (dataSource is null)
        {
            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
            return;
        }

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = _sqlBuilder.BuildDeleteDocumentSql();
            command.Parameters.AddWithValue("document_id", NpgsqlDbType.Uuid, documentId);
            await command.ExecuteNonQueryAsync(ct);
            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
        }
        catch (Exception ex)
        {
            _dataSourceProvider.MarkUnavailable(ex);
            await _fallbackGateway.DeleteDocumentAsync(documentId, ct);
        }
    }
}