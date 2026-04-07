namespace Chatbot.Application.Abstractions;

public sealed class OperationalAuditFeedResult
{
    public IReadOnlyCollection<OperationalAuditFeedItem> Entries { get; set; } = Array.Empty<OperationalAuditFeedItem>();
    public string? NextCursor { get; set; }
}
