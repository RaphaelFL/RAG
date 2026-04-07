namespace Chatbot.Application.Abstractions;

public sealed class ContentSourceDescriptor
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public Stream Content { get; set; } = Stream.Null;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class ExtractedContentResult
{
    public string ExtractorName { get; set; } = string.Empty;
    public string ExtractorVersion { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public string? StructuredJson { get; set; }
    public List<StructuralSpan> Spans { get; set; } = new();
}

public sealed class StructuredExtractionResult
{
    public string ExtractorName { get; set; } = string.Empty;
    public string ExtractorVersion { get; set; } = string.Empty;
    public string StructuredJson { get; set; } = string.Empty;
    public string SemanticText { get; set; } = string.Empty;
}

public sealed class StructuralSpan
{
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public string? WorksheetName { get; set; }
    public int? SlideNumber { get; set; }
    public string Text { get; set; } = string.Empty;
}