namespace Chatbot.Application.Abstractions;

public sealed class ExtractedContentResult
{
    public string ExtractorName { get; set; } = string.Empty;
    public string ExtractorVersion { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public string? StructuredJson { get; set; }
    public List<StructuralSpan> Spans { get; set; } = new();
}
