namespace Chatbot.Domain.Entities;

public class Document
{
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Status { get; set; } = DocumentStatuses.Uploaded;
    public string StoragePath { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public DateTime? IndexedAtUtc { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Categories { get; set; } = new();
}

public class DocumentChunk
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public int PageNumber { get; set; }
    public string? Section { get; set; }
    public int Offset { get; set; }
    public int Length { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}