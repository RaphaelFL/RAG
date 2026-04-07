namespace Chatbot.Infrastructure.Persistence;

internal sealed class CursorToken
{
    public DateTime CreatedAtUtc { get; set; }
    public string Category { get; set; } = string.Empty;
    public string EntryId { get; set; } = string.Empty;
}