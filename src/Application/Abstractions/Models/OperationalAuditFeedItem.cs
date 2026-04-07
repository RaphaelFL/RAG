namespace Chatbot.Application.Abstractions;

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
