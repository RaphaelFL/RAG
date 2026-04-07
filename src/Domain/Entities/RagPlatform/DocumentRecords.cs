namespace Chatbot.Domain.Entities;

public sealed class DocumentRecord
{
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string LogicalName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ParentDocumentHash { get; set; } = string.Empty;
    public string CurrentVersionHash { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string? AccessControlList { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class DocumentVersionRecord
{
    public Guid DocumentVersionId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public int VersionNumber { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string ParentDocumentHash { get; set; } = string.Empty;
    public string StorageUri { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

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

public sealed class ChunkEmbeddingRecord
{
    public Guid ChunkEmbeddingId { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string EmbeddingModelName { get; set; } = string.Empty;
    public string EmbeddingModelVersion { get; set; } = string.Empty;
    public int VectorDimensions { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class IngestionJobRecord
{
    public Guid IngestionJobId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

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