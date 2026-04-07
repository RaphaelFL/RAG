using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Domain.Entities;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence;

public sealed class FileSystemOperationalAuditStore : IOperationalAuditStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly StringComparer CursorComparer = StringComparer.Ordinal;

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ILogger<FileSystemOperationalAuditStore> _logger;
    private readonly string _retrievalLogPath;
    private readonly string _promptAssemblyLogPath;
    private readonly string _agentRunLogPath;
    private readonly string _toolExecutionLogPath;

    public FileSystemOperationalAuditStore(
        IOptions<LocalPersistenceOptions> options,
        IHostEnvironment environment,
        ILogger<FileSystemOperationalAuditStore> logger)
    {
        _logger = logger;

        var basePath = ResolveBasePath(options.Value.BasePath, environment.ContentRootPath);
        var auditPath = Path.Combine(basePath, "operational-audit");
        Directory.CreateDirectory(auditPath);

        _retrievalLogPath = Path.Combine(auditPath, "retrieval-log.jsonl");
        _promptAssemblyLogPath = Path.Combine(auditPath, "prompt-assembly-log.jsonl");
        _agentRunLogPath = Path.Combine(auditPath, "agent-run-log.jsonl");
        _toolExecutionLogPath = Path.Combine(auditPath, "tool-execution-log.jsonl");
    }

    public Task WriteRetrievalLogAsync(RetrievalLogRecord record, CancellationToken ct)
    {
        return AppendAsync(_retrievalLogPath, record, ct);
    }

    public Task WritePromptAssemblyAsync(PromptAssemblyRecord record, CancellationToken ct)
    {
        return AppendAsync(_promptAssemblyLogPath, record, ct);
    }

    public Task WriteAgentRunAsync(AgentRunRecord record, CancellationToken ct)
    {
        return AppendAsync(_agentRunLogPath, record, ct);
    }

    public Task WriteToolExecutionAsync(ToolExecutionRecord record, CancellationToken ct)
    {
        return AppendAsync(_toolExecutionLogPath, record, ct);
    }

    public Task<IReadOnlyCollection<RetrievalLogRecord>> ReadRetrievalLogsAsync(Guid tenantId, int limit, CancellationToken ct)
    {
        return ReadRecordsAsync<RetrievalLogRecord>(_retrievalLogPath, record => record.TenantId == tenantId, limit, ct);
    }

    public Task<IReadOnlyCollection<PromptAssemblyRecord>> ReadPromptAssembliesAsync(Guid tenantId, int limit, CancellationToken ct)
    {
        return ReadRecordsAsync<PromptAssemblyRecord>(_promptAssemblyLogPath, record => record.TenantId == tenantId, limit, ct);
    }

    public Task<IReadOnlyCollection<AgentRunRecord>> ReadAgentRunsAsync(Guid tenantId, int limit, CancellationToken ct)
    {
        return ReadRecordsAsync<AgentRunRecord>(_agentRunLogPath, record => record.TenantId == tenantId, limit, ct);
    }

    public async Task<IReadOnlyCollection<ToolExecutionRecord>> ReadToolExecutionsAsync(Guid tenantId, int limit, CancellationToken ct)
    {
        var scopedAgentRunIds = (await ReadAgentRunsAsync(tenantId, Math.Max(limit * 4, limit), ct))
            .Select(record => record.AgentRunId)
            .ToHashSet();

        return await ReadRecordsAsync<ToolExecutionRecord>(_toolExecutionLogPath, record => scopedAgentRunIds.Contains(record.AgentRunId), limit, ct);
    }

    public async Task<OperationalAuditFeedResult> ReadAuditFeedAsync(OperationalAuditFeedQuery query, CancellationToken ct)
    {
        var boundedLimit = Math.Clamp(query.Limit, 1, 100);
        var retrievals = await ReadRetrievalLogsAsync(query.TenantId, int.MaxValue, ct);
        var prompts = await ReadPromptAssembliesAsync(query.TenantId, int.MaxValue, ct);
        var agentRuns = await ReadAgentRunsAsync(query.TenantId, int.MaxValue, ct);
        var toolExecutions = await ReadToolExecutionsAsync(query.TenantId, int.MaxValue, ct);

        var entries = retrievals.Select(MapRetrieval)
            .Concat(prompts.Select(MapPromptAssembly))
            .Concat(agentRuns.Select(MapAgentRun))
            .Concat(toolExecutions.Select(MapToolExecution))
            .Where(entry => MatchesCategory(entry, query.Category))
            .Where(entry => MatchesStatus(entry, query.Status))
            .Where(entry => MatchesDateRange(entry, query.FromUtc, query.ToUtc))
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ThenByDescending(entry => entry.Category, CursorComparer)
            .ThenByDescending(entry => entry.EntryId, CursorComparer)
            .ToList();

        var cursorToken = TryDecodeCursor(query.Cursor);
        if (cursorToken is not null)
        {
            entries = entries
                .Where(entry => IsAfterCursor(entry, cursorToken))
                .ToList();
        }

        var pageWithLookahead = entries.Take(boundedLimit + 1).ToArray();
        var hasMore = pageWithLookahead.Length > boundedLimit;
        var page = pageWithLookahead.Take(boundedLimit).ToArray();
        return new OperationalAuditFeedResult
        {
            Entries = page,
            NextCursor = hasMore ? EncodeCursor(page[^1]) : null
        };
    }

    private async Task AppendAsync<TRecord>(string path, TRecord record, CancellationToken ct)
    {
        try
        {
            await _writeLock.WaitAsync(ct);
            await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await using var writer = new StreamWriter(stream);
            await writer.WriteLineAsync(JsonSerializer.Serialize(record, SerializerOptions));
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

    private async Task<IReadOnlyCollection<TRecord>> ReadRecordsAsync<TRecord>(string path, Func<TRecord, bool> predicate, int limit, CancellationToken ct)
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
                .Select(line => TryDeserialize<TRecord>(line))
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
            return JsonSerializer.Deserialize<TRecord>(line, SerializerOptions);
        }
        catch
        {
            return default;
        }
    }

    private static OperationalAuditFeedItem MapRetrieval(RetrievalLogRecord record)
    {
        return new OperationalAuditFeedItem
        {
            EntryId = record.RetrievalLogId.ToString("N"),
            Category = "retrieval",
            Status = "completed",
            Title = record.QueryText,
            Summary = $"{record.Strategy} | topK {record.ReturnedTopK}/{record.RequestedTopK}",
            DetailsJson = JsonSerializer.Serialize(new { record.FiltersJson, record.DiagnosticsJson }, SerializerOptions),
            CreatedAtUtc = record.CreatedAtUtc
        };
    }

    private static OperationalAuditFeedItem MapPromptAssembly(PromptAssemblyRecord record)
    {
        return new OperationalAuditFeedItem
        {
            EntryId = record.PromptAssemblyId.ToString("N"),
            Category = "prompt-assembly",
            Status = "completed",
            Title = record.PromptTemplateId,
            Summary = $"tokens {record.UsedPromptTokens}/{record.MaxPromptTokens}",
            DetailsJson = JsonSerializer.Serialize(new { record.IncludedChunkIdsJson, record.PromptBody }, SerializerOptions),
            CreatedAtUtc = record.CreatedAtUtc
        };
    }

    private static OperationalAuditFeedItem MapAgentRun(AgentRunRecord record)
    {
        return new OperationalAuditFeedItem
        {
            EntryId = record.AgentRunId.ToString("N"),
            Category = "agent-run",
            Status = record.Status,
            Title = record.AgentName,
            Summary = $"budget restante {record.RemainingBudget}/{record.ToolBudget}",
            DetailsJson = JsonSerializer.Serialize(new { record.InputJson, record.OutputJson }, SerializerOptions),
            CreatedAtUtc = record.CreatedAtUtc,
            CompletedAtUtc = record.CompletedAtUtc
        };
    }

    private static OperationalAuditFeedItem MapToolExecution(ToolExecutionRecord record)
    {
        return new OperationalAuditFeedItem
        {
            EntryId = record.ToolExecutionId.ToString("N"),
            Category = "tool-execution",
            Status = record.Status,
            Title = record.ToolName,
            Summary = $"agent run {record.AgentRunId:N}",
            DetailsJson = JsonSerializer.Serialize(new { record.InputJson, record.OutputJson }, SerializerOptions),
            CreatedAtUtc = record.CreatedAtUtc,
            CompletedAtUtc = record.CompletedAtUtc
        };
    }

    private static bool MatchesCategory(OperationalAuditFeedItem entry, string? category)
    {
        return string.IsNullOrWhiteSpace(category)
            || string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesStatus(OperationalAuditFeedItem entry, string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(entry.Status)
            && string.Equals(entry.Status, status, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDateRange(OperationalAuditFeedItem entry, DateTime? fromUtc, DateTime? toUtc)
    {
        return (!fromUtc.HasValue || entry.CreatedAtUtc >= fromUtc.Value)
            && (!toUtc.HasValue || entry.CreatedAtUtc <= toUtc.Value);
    }

    private static string EncodeCursor(OperationalAuditFeedItem entry)
    {
        var payload = JsonSerializer.Serialize(new CursorToken
        {
            CreatedAtUtc = entry.CreatedAtUtc,
            Category = entry.Category,
            EntryId = entry.EntryId
        }, SerializerOptions);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    private static CursorToken? TryDecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return JsonSerializer.Deserialize<CursorToken>(payload, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsAfterCursor(OperationalAuditFeedItem entry, CursorToken cursor)
    {
        if (entry.CreatedAtUtc < cursor.CreatedAtUtc)
        {
            return true;
        }

        if (entry.CreatedAtUtc > cursor.CreatedAtUtc)
        {
            return false;
        }

        var categoryCompare = CursorComparer.Compare(entry.Category, cursor.Category);
        if (categoryCompare < 0)
        {
            return true;
        }

        if (categoryCompare > 0)
        {
            return false;
        }

        return CursorComparer.Compare(entry.EntryId, cursor.EntryId) < 0;
    }

    private static string ResolveBasePath(string configuredPath, string contentRootPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }
}