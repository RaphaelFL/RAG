namespace Chatbot.Application.Contracts;

public sealed class OperationalAuditFeedResponseDto
{
    public List<OperationalAuditEntryDto> Entries { get; set; } = new();
    public string? NextCursor { get; set; }
}
