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
    Task<ChatSessionSnapshot?> GetAsync(Guid sessionId, Guid tenantId, CancellationToken ct);
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
    Task<IReadOnlyList<DocumentDetailsDto>> ListDocumentsAsync(CancellationToken ct);
    Task<DocumentDetailsDto?> GetDocumentAsync(Guid documentId, CancellationToken ct);
    Task<DocumentInspectionDto?> GetDocumentInspectionAsync(Guid documentId, string? search, int pageNumber, int pageSize, CancellationToken ct);
    Task<DocumentChunkEmbeddingDto?> GetDocumentChunkEmbeddingAsync(Guid documentId, string chunkId, CancellationToken ct);
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
    Task<DocumentParseResultDto?> ParseAsync(IngestDocumentCommand command, CancellationToken ct);
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
    Task<List<DocumentChunkIndexDto>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct);
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
