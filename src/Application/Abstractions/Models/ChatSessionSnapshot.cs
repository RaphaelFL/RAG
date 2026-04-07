namespace Chatbot.Application.Abstractions;

public class ChatSessionSnapshot
{
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public List<ChatSessionMessageSnapshot> Messages { get; set; } = new();
}
