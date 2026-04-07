namespace Chatbot.Domain.Entities;

public class ChatSession
{
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public List<ChatMessage> Messages { get; set; } = new();
}
