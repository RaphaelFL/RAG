namespace Chatbot.Application.Abstractions;

public class PromptTemplateDefinition
{
    public string TemplateId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string InsufficientEvidenceMessage { get; set; } = string.Empty;
}
