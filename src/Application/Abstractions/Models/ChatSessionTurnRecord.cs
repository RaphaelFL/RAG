namespace Chatbot.Application.Abstractions;

public class ChatSessionTurnRecord
{
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid AnswerId { get; set; }
    public string UserMessage { get; set; } = string.Empty;
    public string AssistantMessage { get; set; } = string.Empty;
    public IReadOnlyCollection<CitationDto> Citations { get; set; } = Array.Empty<CitationDto>();
    public UsageMetadataDto Usage { get; set; } = new();
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
}
