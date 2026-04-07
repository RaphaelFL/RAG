namespace Chatbot.Application.Abstractions;

public class DocumentCatalogEntry
{
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? Source { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? ExternalId { get; set; }
    public string? AccessPolicy { get; set; }
    public string? StoragePath { get; set; }
    public string? QuarantinePath { get; set; }
    public Guid? LastJobId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int IndexedChunkCount { get; set; }
    public List<DocumentChunkIndexDto> Chunks { get; set; } = new();
}
