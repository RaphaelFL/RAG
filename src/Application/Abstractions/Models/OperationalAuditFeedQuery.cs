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
