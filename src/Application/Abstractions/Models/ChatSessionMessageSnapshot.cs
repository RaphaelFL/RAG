namespace Chatbot.Application.Abstractions;

public class ChatSessionMessageSnapshot
{
    public Guid MessageId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public IReadOnlyCollection<CitationDto> Citations { get; set; } = Array.Empty<CitationDto>();
    public UsageMetadataDto? Usage { get; set; }
    public string? TemplateVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
