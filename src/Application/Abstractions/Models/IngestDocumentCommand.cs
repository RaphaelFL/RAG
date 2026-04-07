namespace Chatbot.Application.Abstractions;

public class IngestDocumentCommand
{
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long ContentLength { get; set; }
    public string? DocumentTitle { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? Source { get; set; }
    public string? ExternalId { get; set; }
    public string? AccessPolicy { get; set; }
    public Stream Content { get; set; } = Stream.Null;
}
