namespace Chatbot.Application.Contracts;

public class ChatResponseDto
{
    public Guid AnswerId { get; set; }
    public Guid SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<CitationDto> Citations { get; set; } = new();
    public UsageMetadataDto Usage { get; set; } = new();
    public ChatPolicyDto Policy { get; set; } = new();
    public DateTime TimestampUtc { get; set; }
}
