namespace Chatbot.Application.Abstractions;

public sealed class OperationalAuditFeedQuery
{
    public Guid TenantId { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Cursor { get; set; }
    public int Limit { get; set; } = 20;
}

public sealed class OperationalAuditFeedResult
{
    public IReadOnlyCollection<OperationalAuditFeedItem> Entries { get; set; } = Array.Empty<OperationalAuditFeedItem>();
    public string? NextCursor { get; set; }
}

public sealed class OperationalAuditFeedItem
{
    public string EntryId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? DetailsJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}