namespace Chatbot.Domain.Entities;

public class ChatMessage
{
    public Guid MessageId { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = new();
    public UsageMetadata? Usage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
