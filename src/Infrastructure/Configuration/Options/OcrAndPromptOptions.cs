namespace Chatbot.Infrastructure.Configuration;

public sealed class OcrOptions
{
    public string PrimaryProvider { get; set; } = string.Empty;
    public string FallbackProvider { get; set; } = string.Empty;
    public bool EnableFallback { get; set; }
    public bool EnableSelectiveOcr { get; set; } = true;
    public int MinimumDirectTextCharacters { get; set; } = 120;
    public double MinimumDirectTextCoverageRatio { get; set; } = 0.02;
}

public sealed class PromptTemplateOptions
{
    public string GroundedAnswerVersion { get; set; } = string.Empty;
    public int DefaultTimeout { get; set; }
    public string InsufficientEvidenceMessage { get; set; } = string.Empty;
    public string[] BlockedInputPatterns { get; set; } = Array.Empty<string>();
}