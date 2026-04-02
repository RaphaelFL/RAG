namespace Chatbot.Application.Abstractions;

public delegate ValueTask BackgroundWorkItem(IServiceProvider serviceProvider, CancellationToken ct);

public interface IChatOrchestrator
{
    Task<ChatResponseDto> SendAsync(ChatRequestDto request, CancellationToken ct);
    IAsyncEnumerable<StreamingChatEventDto> StreamAsync(ChatRequestDto request, CancellationToken ct);
}

public interface IChatCompletionProvider
{
    Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct);
}

public interface IAgenticChatPlanner
{
    AgenticChatPlan CreatePlan(ChatRequestDto request);
}

public interface IPromptTemplateRegistry
{
    PromptTemplateDefinition GetRequired(string templateId, string? templateVersion = null);
    IReadOnlyCollection<PromptTemplateDefinition> ListAll();
}

public interface IPromptInjectionDetector
{
    bool TryDetect(string? input, out string pattern);
}

public interface IChatSessionStore
{
    Task AppendTurnAsync(ChatSessionTurnRecord record, CancellationToken ct);
    ChatSessionSnapshot? Get(Guid sessionId, Guid tenantId);
}

public interface IMalwareScanner
{
    Task<MalwareScanResultDto> ScanAsync(IngestDocumentCommand command, CancellationToken ct);
}

public interface IRetrievalService
{
    Task<RetrievalResultDto> RetrieveAsync(RetrievalQueryDto query, CancellationToken ct);
    Task<SearchQueryResponseDto> QueryAsync(SearchQueryRequestDto query, CancellationToken ct);
}

public interface IIngestionPipeline
{
    Task<UploadDocumentResponseDto> IngestAsync(IngestDocumentCommand command, CancellationToken ct);
    Task<ReindexDocumentResponseDto> ReindexAsync(Guid documentId, bool fullReindex, CancellationToken ct);
    Task<BulkReindexResponseDto> ReindexAsync(BulkReindexRequestDto request, Guid tenantId, CancellationToken ct);
    Task<DocumentDetailsDto?> GetDocumentAsync(Guid documentId, CancellationToken ct);
}

public interface IDocumentMetadataSuggestionService
{
    Task<DocumentMetadataSuggestionDto> SuggestAsync(IngestDocumentCommand command, CancellationToken ct);
}

public interface IIngestionJobProcessor
{
    Task ProcessIngestionAsync(IngestionBackgroundJob job, CancellationToken ct);
    Task ProcessReindexAsync(ReindexBackgroundJob job, CancellationToken ct);
}

public interface IBackgroundJobQueue
{
    ValueTask EnqueueAsync(BackgroundWorkItem workItem, CancellationToken ct = default);
    ValueTask<BackgroundWorkItem> DequeueAsync(CancellationToken ct);
}

public interface IOcrProvider
{
    Task<OcrResultDto> ExtractAsync(Stream content, string fileName, CancellationToken ct);
    string ProviderName { get; }
}

public interface IDocumentParser
{
    bool CanParse(IngestDocumentCommand command);
    Task<string?> ParseAsync(IngestDocumentCommand command, CancellationToken ct);
}

public interface IDocumentTextExtractor
{
    Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct);
}

public interface IEmbeddingProvider
{
    Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct);
}

public interface IRequestContextAccessor
{
    Guid? TenantId { get; set; }
    string? UserId { get; set; }
    string? UserRole { get; set; }
}

public interface IDocumentAuthorizationService
{
    bool CanAccess(DocumentCatalogEntry document, Guid? tenantId, string? userId, string? userRole);
}

public interface IFeatureFlagService
{
    bool IsSemanticRankingEnabled { get; }
    bool IsGraphRagEnabled { get; }
    bool IsMcpEnabled { get; }
}

public interface IChunkingStrategy
{
    List<DocumentChunkIndexDto> Chunk(IngestDocumentCommand command, DocumentTextExtractionResultDto extraction);
}

public interface ICitationAssembler
{
    List<CitationDto> Assemble(IReadOnlyCollection<RetrievedChunkDto> chunks, int maxCitations);
}

public interface IDocumentCatalog
{
    void Upsert(DocumentCatalogEntry entry);
    DocumentCatalogEntry? Get(Guid documentId);
    IReadOnlyCollection<DocumentCatalogEntry> Query(FileSearchFilterDto? filters);
    DocumentCatalogEntry? FindByContentHash(Guid tenantId, string contentHash);
}

public interface ISecurityAuditLogger
{
    void LogAuthenticationFailure(string? userId, string reason);
    void LogAccessDenied(string? userId, string resource);
    void LogFileRejected(string fileName, string reason);
    void LogProviderFallback(string provider, string fallbackProvider, string reason);
    void LogPromptInjectionDetected(string source, string reason);
}

public interface IBlobStorageGateway
{
    Task<string> SaveAsync(Stream content, string path, CancellationToken ct);
    Task<Stream> GetAsync(string path, CancellationToken ct);
    Task DeleteAsync(string path, CancellationToken ct);
}

public interface ISearchIndexGateway
{
    Task IndexDocumentChunksAsync(List<DocumentChunkIndexDto> chunks, CancellationToken ct);
    Task<List<SearchResultDto>> HybridSearchAsync(string query, float[]? queryEmbedding, int topK, FileSearchFilterDto? filters, CancellationToken ct);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken ct);
}

public interface IApplicationCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
}

public interface IRagRuntimeSettings
{
    int DenseChunkSize { get; }
    int DenseOverlap { get; }
    int NarrativeChunkSize { get; }
    int NarrativeOverlap { get; }
    int MinimumChunkCharacters { get; }
    int RetrievalCandidateMultiplier { get; }
    int RetrievalMaxCandidateCount { get; }
    int MaxContextChunks { get; }
    double MinimumRerankScore { get; }
    double ExactMatchBoost { get; }
    double TitleMatchBoost { get; }
    double FilterMatchBoost { get; }
    TimeSpan RetrievalCacheTtl { get; }
    TimeSpan ChatCompletionCacheTtl { get; }
    TimeSpan EmbeddingCacheTtl { get; }
}

public interface IRagRuntimeAdministrationService
{
    RagRuntimeSettingsDto GetSettings();
    RagRuntimeSettingsDto UpdateSettings(UpdateRagRuntimeSettingsDto request);
}

// DTOs para abstrações internas
public class OcrResultDto
{
    public string ExtractedText { get; set; } = string.Empty;
    public List<PageExtractionDto> Pages { get; set; } = new();
    public string? Provider { get; set; }
}

public class DocumentTextExtractionResultDto
{
    public string Text { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public List<PageExtractionDto> Pages { get; set; } = new();
}

public class PageExtractionDto
{
    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public List<TableDto>? Tables { get; set; }
}

public class TableDto
{
    public List<List<string>> Rows { get; set; } = new();
}

public class DocumentChunkIndexDto
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public int PageNumber { get; set; }
    public string? Section { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class SearchResultDto
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class FileSearchFilterDto
{
    public List<Guid>? DocumentIds { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Categories { get; set; }
    public List<string>? ContentTypes { get; set; }
    public List<string>? Sources { get; set; }
    public Guid? TenantId { get; set; }
}

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
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    public string RawHash { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
}

public sealed class ReindexBackgroundJob
{
    public Guid JobId { get; set; }
    public Guid DocumentId { get; set; }
    public bool FullReindex { get; set; }
    public string? ForceEmbeddingModel { get; set; }
}

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
    public List<DocumentChunkIndexDto> Chunks { get; set; } = new();
}

public class PromptTemplateDefinition
{
    public string TemplateId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string InsufficientEvidenceMessage { get; set; } = string.Empty;
}

public sealed class AgenticChatPlan
{
    public bool RequiresRetrieval { get; init; }
    public bool AllowsGeneralKnowledge { get; init; }
    public bool PreferStreaming { get; init; }
    public string ExecutionMode { get; init; } = "grounded";
}

public sealed class ChatCompletionRequest
{
    public string Message { get; set; } = string.Empty;
    public PromptTemplateDefinition Template { get; set; } = new();
    public bool AllowGeneralKnowledge { get; set; }
    public IReadOnlyCollection<RetrievedChunkDto> RetrievedChunks { get; set; } = Array.Empty<RetrievedChunkDto>();
}

public sealed class ChatCompletionResult
{
    public string Message { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public class ChatSessionTurnRecord
{
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid AnswerId { get; set; }
    public string UserMessage { get; set; } = string.Empty;
    public string AssistantMessage { get; set; } = string.Empty;
    public IReadOnlyCollection<CitationDto> Citations { get; set; } = Array.Empty<CitationDto>();
    public UsageMetadataDto Usage { get; set; } = new();
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
}

public class ChatSessionSnapshot
{
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public List<ChatSessionMessageSnapshot> Messages { get; set; } = new();
}

public class ChatSessionMessageSnapshot
{
    public Guid MessageId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public IReadOnlyCollection<CitationDto> Citations { get; set; } = Array.Empty<CitationDto>();
    public UsageMetadataDto? Usage { get; set; }
    public string? TemplateVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class MalwareScanResultDto
{
    public bool IsSafe { get; set; }
    public bool RequiresQuarantine { get; set; }
    public string? Reason { get; set; }
}

public sealed class DuplicateDocumentException : Exception
{
    public DuplicateDocumentException(string message) : base(message)
    {
    }
}
