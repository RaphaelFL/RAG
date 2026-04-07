namespace Chatbot.Infrastructure.Configuration;

public sealed class PromptTemplateOptions
{
    public string GroundedAnswerVersion { get; set; } = string.Empty;
    public int DefaultTimeout { get; set; }
    public string InsufficientEvidenceMessage { get; set; } = string.Empty;
    public string[] BlockedInputPatterns { get; set; } = Array.Empty<string>();
}
