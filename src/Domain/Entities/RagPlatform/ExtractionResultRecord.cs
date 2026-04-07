namespace Chatbot.Domain.Entities;

public sealed class ExtractionResultRecord
{
    public Guid ExtractionResultId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string ExtractorName { get; set; } = string.Empty;
    public string ExtractorVersion { get; set; } = string.Empty;
    public string StructuredJson { get; set; } = string.Empty;
    public string SemanticText { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
