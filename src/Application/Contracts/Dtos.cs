namespace Chatbot.Application.Contracts;

/// <summary>
/// DTO para requisição de chat
/// </summary>
public class ChatRequestDto
{
    public Guid SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TemplateId { get; set; } = "grounded_answer";
    public string TemplateVersion { get; set; } = "1.0.0";
    public ChatFiltersDto? Filters { get; set; }
    public ChatOptionsDto? Options { get; set; }
}

public class ChatFiltersDto
{
    public List<Guid>? DocumentIds { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Categories { get; set; }
    public List<string>? ContentTypes { get; set; }
    public List<string>? Sources { get; set; }
}

public class ChatOptionsDto
{
    public int MaxCitations { get; set; } = 5;
    public bool AllowGeneralKnowledge { get; set; } = true;
    public bool SemanticRanking { get; set; } = true;
}

/// <summary>
/// DTO para resposta de chat
/// </summary>
public class ChatResponseDto
{
    public Guid AnswerId { get; set; }
    public Guid SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<CitationDto> Citations { get; set; } = new();
    public UsageMetadataDto Usage { get; set; } = new();
    public ChatPolicyDto Policy { get; set; } = new();
    public DateTime TimestampUtc { get; set; }
}

public class CitationDto
{
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string ChunkId { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public LocationDto? Location { get; set; }
    public double Score { get; set; }
}

public class LocationDto
{
    public int? Page { get; set; }
    public int? EndPage { get; set; }
    public string? Section { get; set; }
}

public class UsageMetadataDto
{
    public string Model { get; set; } = "gpt-4.1";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public long LatencyMs { get; set; }
    public string RetrievalStrategy { get; set; } = "hybrid";
    public Dictionary<string, long> RuntimeMetrics { get; set; } = new();
}

public class ChatPolicyDto
{
    public bool Grounded { get; set; }
    public bool HadEnoughEvidence { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = "1.0.0";
}

/// <summary>
/// DTO para streaming de eventos SSE
/// </summary>
public class StreamingChatEventDto
{
    public string EventType { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public class StreamStartedEventDto
{
    public Guid AnswerId { get; set; }
    public Guid SessionId { get; set; }
}

public class StreamDeltaEventDto
{
    public string Text { get; set; } = string.Empty;
}

public class StreamCompletedEventDto
{
    public UsageMetadataDto Usage { get; set; } = new();
}

public class StreamErrorEventDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
}

/// <summary>
/// DTO global de erro
/// </summary>
public class ErrorResponseDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string[]>? Details { get; set; }
    public string TraceId { get; set; } = string.Empty;
}

/// <summary>
/// DTO para upload de documento
/// </summary>
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

/// <summary>
/// DTO para reindexação
/// </summary>
public class ReindexDocumentRequestDto
{
    public Guid DocumentId { get; set; }
    public bool FullReindex { get; set; } = false;
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
    public string ContentType { get; set; } = string.Empty;
    public string? Source { get; set; }
    public Guid? LastJobId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DocumentMetadataDto Metadata { get; set; } = new();
}

public class DocumentMetadataDto
{
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? ExternalId { get; set; }
    public string? AccessPolicy { get; set; }
}

/// <summary>
/// DTO para recuperação e busca
/// </summary>
public class RetrievalQueryDto
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
    public List<Guid>? DocumentIds { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Categories { get; set; }
    public List<string>? ContentTypes { get; set; }
    public List<string>? Sources { get; set; }
    public bool SemanticRanking { get; set; } = true;
}

public class RetrievalResultDto
{
    public List<RetrievedChunkDto> Chunks { get; set; } = new();
    public string RetrievalStrategy { get; set; } = "hybrid";
    public long LatencyMs { get; set; }
}

public class SearchQueryRequestDto
{
    public string Query { get; set; } = string.Empty;
    public SearchFiltersDto? Filters { get; set; }
    public int Top { get; set; } = 5;
    public bool SemanticRanking { get; set; } = true;
}

public class SearchFiltersDto
{
    public List<Guid>? DocumentIds { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Categories { get; set; }
    public List<string>? ContentTypes { get; set; }
    public List<string>? Sources { get; set; }
}

public class RagRuntimeSettingsDto
{
    public int DenseChunkSize { get; set; }
    public int DenseOverlap { get; set; }
    public int NarrativeChunkSize { get; set; }
    public int NarrativeOverlap { get; set; }
    public int MinimumChunkCharacters { get; set; }
    public int RetrievalCandidateMultiplier { get; set; }
    public int RetrievalMaxCandidateCount { get; set; }
    public int MaxContextChunks { get; set; }
    public double MinimumRerankScore { get; set; }
    public double ExactMatchBoost { get; set; }
    public double TitleMatchBoost { get; set; }
    public double FilterMatchBoost { get; set; }
    public int RetrievalCacheTtlSeconds { get; set; }
    public int ChatCompletionCacheTtlSeconds { get; set; }
    public int EmbeddingCacheTtlHours { get; set; }
}

public class UpdateRagRuntimeSettingsDto
{
    public int DenseChunkSize { get; set; }
    public int DenseOverlap { get; set; }
    public int NarrativeChunkSize { get; set; }
    public int NarrativeOverlap { get; set; }
    public int MinimumChunkCharacters { get; set; }
    public int RetrievalCandidateMultiplier { get; set; }
    public int RetrievalMaxCandidateCount { get; set; }
    public int MaxContextChunks { get; set; }
    public double MinimumRerankScore { get; set; }
    public double ExactMatchBoost { get; set; }
    public double TitleMatchBoost { get; set; }
    public double FilterMatchBoost { get; set; }
    public int RetrievalCacheTtlSeconds { get; set; }
    public int ChatCompletionCacheTtlSeconds { get; set; }
    public int EmbeddingCacheTtlHours { get; set; }
}

public class SearchQueryResponseDto
{
    public List<SearchQueryItemDto> Items { get; set; } = new();
    public int Count { get; set; }
}

public class SearchQueryItemDto
{
    public Guid DocumentId { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public double Score { get; set; }
}

public class RetrievedChunkDto
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
    public int PageNumber { get; set; }
    public int EndPageNumber { get; set; }
    public string? Section { get; set; }
}
