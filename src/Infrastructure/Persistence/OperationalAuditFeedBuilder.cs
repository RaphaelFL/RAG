using System.Text;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Domain.Entities;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class OperationalAuditFeedBuilder
{
    private static readonly StringComparer CursorComparer = StringComparer.Ordinal;

    public OperationalAuditFeedResult Build(
        OperationalAuditFeedQuery query,
        IReadOnlyCollection<RetrievalLogRecord> retrievals,
        IReadOnlyCollection<PromptAssemblyRecord> prompts,
        IReadOnlyCollection<AgentRunRecord> agentRuns,
        IReadOnlyCollection<ToolExecutionRecord> toolExecutions)
    {
        var boundedLimit = Math.Clamp(query.Limit, 1, 100);
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

    private static OperationalAuditFeedItem MapRetrieval(RetrievalLogRecord record)
    {
        return new OperationalAuditFeedItem
        {
            EntryId = record.RetrievalLogId.ToString("N"),
            Category = "retrieval",
            Status = "completed",
            Title = record.QueryText,
            Summary = $"{record.Strategy} | topK {record.ReturnedTopK}/{record.RequestedTopK}",
            DetailsJson = JsonSerializer.Serialize(new { record.FiltersJson, record.DiagnosticsJson }, OperationalAuditJsonSerializer.Options),
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
            DetailsJson = JsonSerializer.Serialize(new { record.IncludedChunkIdsJson, record.PromptBody }, OperationalAuditJsonSerializer.Options),
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
            DetailsJson = JsonSerializer.Serialize(new { record.InputJson, record.OutputJson }, OperationalAuditJsonSerializer.Options),
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
            DetailsJson = JsonSerializer.Serialize(new { record.InputJson, record.OutputJson }, OperationalAuditJsonSerializer.Options),
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
        }, OperationalAuditJsonSerializer.Options);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    private static CursorToken? TryDecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return JsonSerializer.Deserialize<CursorToken>(payload, OperationalAuditJsonSerializer.Options);
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
}