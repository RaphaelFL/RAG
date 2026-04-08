using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class JsonLinesOperationalAuditStorage
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ILogger _logger;

    public JsonLinesOperationalAuditStorage(ILogger logger)
    {
        _logger = logger;
    }

    public async Task AppendAsync<TRecord>(string path, TRecord record, CancellationToken ct)
    {
        try
        {
            await _writeLock.WaitAsync(ct);
            await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await using var writer = new StreamWriter(stream);
            await writer.WriteLineAsync(JsonSerializer.Serialize(record, OperationalAuditJsonSerializer.Options));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao persistir trilha operacional em {AuditPath}.", path);
        }
        finally
        {
            if (_writeLock.CurrentCount == 0)
            {
                _writeLock.Release();
            }
        }
    }

    public async Task<IReadOnlyCollection<TRecord>> ReadRecordsAsync<TRecord>(string path, Func<TRecord, bool> predicate, int limit, CancellationToken ct)
    {
        if (!File.Exists(path) || limit <= 0)
        {
            return Array.Empty<TRecord>();
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(path, ct);
            return lines
                .Reverse()
                .Select(TryDeserialize<TRecord>)
                .Where(record => record is not null)
                .Select(record => record!)
                .Where(predicate)
                .Take(limit)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler trilha operacional em {AuditPath}.", path);
            return Array.Empty<TRecord>();
        }
    }

    private static TRecord? TryDeserialize<TRecord>(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<TRecord>(line, OperationalAuditJsonSerializer.Options);
        }
        catch
        {
            return default;
        }
    }
}