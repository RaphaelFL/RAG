namespace Chatbot.Domain.Entities;

public sealed class ConversationMessageRecord
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
