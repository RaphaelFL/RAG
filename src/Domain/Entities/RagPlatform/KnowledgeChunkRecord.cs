namespace Chatbot.Domain.Entities;

public sealed class KnowledgeChunkRecord
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public Guid TenantId { get; set; }
    public int ChunkIndex { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string ParentDocumentHash { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public string? WorksheetName { get; set; }
    public int? SlideNumber { get; set; }
    public string? SectionTitle { get; set; }
    public string? TableId { get; set; }
    public string? FormId { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? AccessControlList { get; set; }
    public string Text { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? EntitiesJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
