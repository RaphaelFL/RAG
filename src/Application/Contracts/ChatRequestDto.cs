namespace Chatbot.Application.Contracts;

public class ChatRequestDto
{
    public Guid SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TemplateId { get; set; } = "grounded_answer";
    public string TemplateVersion { get; set; } = "1.0.0";
    public ChatFiltersDto? Filters { get; set; }
    public ChatOptionsDto? Options { get; set; }
}
