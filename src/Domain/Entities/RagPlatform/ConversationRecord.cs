namespace Chatbot.Domain.Entities;

public sealed class ConversationRecord
{
    public Guid ConversationId { get; set; }
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
