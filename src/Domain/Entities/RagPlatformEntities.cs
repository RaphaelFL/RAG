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

public sealed class RetrievalLogRecord
{
    public Guid RetrievalLogId { get; set; }
    public Guid TenantId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public int RequestedTopK { get; set; }
    public int ReturnedTopK { get; set; }
    public string? FiltersJson { get; set; }
    public string? DiagnosticsJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class PromptAssemblyRecord
{
    public Guid PromptAssemblyId { get; set; }
    public Guid TenantId { get; set; }
    public string PromptTemplateId { get; set; } = string.Empty;
    public int MaxPromptTokens { get; set; }
    public int UsedPromptTokens { get; set; }
    public string? IncludedChunkIdsJson { get; set; }
    public string PromptBody { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class ConversationRecord
{
    public Guid ConversationId { get; set; }
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class ConversationMessageRecord
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AgentRunRecord
{
    public Guid AgentRunId { get; set; }
    public Guid TenantId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ToolBudget { get; set; }
    public int RemainingBudget { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class ToolExecutionRecord
{
    public Guid ToolExecutionId { get; set; }
    public Guid AgentRunId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class CitationRecord
{
    public Guid CitationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string HumanReadableLocation { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}