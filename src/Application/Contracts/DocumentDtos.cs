namespace Chatbot.Application.Contracts;

public class UploadDocumentRequestDto
{
    public string Name { get; set; } = string.Empty;
    public List<string>? Tags { get; set; }
    public List<string>? Categories { get; set; }
}

public class UploadDocumentResponseDto
{
    public Guid DocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid IngestionJobId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class DocumentMetadataSuggestionDto
{
    public string SuggestedTitle { get; set; } = string.Empty;
    public string? SuggestedCategory { get; set; }
    public List<string> SuggestedCategories { get; set; } = new();
    public List<string> SuggestedTags { get; set; } = new();
    public string Strategy { get; set; } = string.Empty;
    public string PreviewText { get; set; } = string.Empty;
}

public class ReindexDocumentRequestDto
{
    public Guid DocumentId { get; set; }
    public bool FullReindex { get; set; }
}

public class ReindexDocumentResponseDto
{
    public Guid DocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ChunksReindexed { get; set; }
    public Guid? JobId { get; set; }
}

public class BulkReindexRequestDto
{
    public List<Guid> DocumentIds { get; set; } = new();
    public bool IncludeAllTenantDocuments { get; set; }
    public string Mode { get; set; } = "incremental";
    public string? Reason { get; set; }
    public string? ForceEmbeddingModel { get; set; }
}

public class BulkReindexResponseDto
{
    public bool Accepted { get; set; }
    public Guid JobId { get; set; }
    public string Mode { get; set; } = "incremental";
    public int DocumentCount { get; set; }
}

public class DocumentDetailsDto
{
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public int IndexedChunkCount { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? Source { get; set; }
    public Guid? LastJobId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DocumentMetadataDto Metadata { get; set; } = new();
}

public class DocumentInspectionDto
{
    public DocumentDetailsDto Document { get; set; } = new();
    public int EmbeddedChunkCount { get; set; }
    public int TotalChunkCount { get; set; }
    public int FilteredChunkCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<DocumentChunkInspectionDto> Chunks { get; set; } = new();
}

public class DocumentChunkInspectionDto
{
    public string ChunkId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int CharacterCount { get; set; }
    public int PageNumber { get; set; }
    public int? EndPageNumber { get; set; }
    public string? Section { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DocumentEmbeddingInspectionDto Embedding { get; set; } = new();
}

public class DocumentEmbeddingInspectionDto
{
    public bool Exists { get; set; }
    public int Dimensions { get; set; }
    public List<float> Preview { get; set; } = new();
}

public class DocumentChunkEmbeddingDto
{
    public Guid DocumentId { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public List<float> Values { get; set; } = new();
}

public class DocumentMetadataDto
{
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? ExternalId { get; set; }
    public string? AccessPolicy { get; set; }
}