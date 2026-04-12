namespace Chatbot.Application.Abstractions;

public sealed class IngestionBackgroundJob
{
    public Guid JobId { get; set; }
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
    public string? ClientExtractedText { get; set; }
    public List<PageExtractionDto> ClientExtractedPages { get; set; } = new();
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    public string RawHash { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
}
