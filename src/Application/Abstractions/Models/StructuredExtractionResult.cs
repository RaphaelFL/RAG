namespace Chatbot.Application.Abstractions;

public sealed class StructuredExtractionResult
{
    public string ExtractorName { get; set; } = string.Empty;
    public string ExtractorVersion { get; set; } = string.Empty;
    public string StructuredJson { get; set; } = string.Empty;
    public string SemanticText { get; set; } = string.Empty;
}
