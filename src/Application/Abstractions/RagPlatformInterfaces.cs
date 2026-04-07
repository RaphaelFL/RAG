using Chatbot.Domain.Entities;

namespace Chatbot.Application.Abstractions;

public interface IContentExtractor
{
    bool CanHandle(ContentSourceDescriptor source);
    Task<ExtractedContentResult> ExtractAsync(ContentSourceDescriptor source, CancellationToken ct);
}

public interface IStructuredExtractor
{
    bool CanHandle(ExtractedContentResult content);
    Task<StructuredExtractionResult> ExtractAsync(ExtractedContentResult content, CancellationToken ct);
}

public interface IChunker
{
    string StrategyName { get; }
    Task<IReadOnlyCollection<ChunkCandidate>> ChunkAsync(ChunkingRequest request, CancellationToken ct);
}

public interface IEmbeddingModel
{
    string ModelName { get; }
    string ModelVersion { get; }
    int Dimensions { get; }
    Task<IReadOnlyCollection<float[]>> GenerateAsync(IReadOnlyCollection<string> texts, CancellationToken ct);
}

public interface IEmbeddingGenerationService
{
    Task<IReadOnlyCollection<EmbeddingEnvelope>> GenerateBatchAsync(EmbeddingBatchRequest request, CancellationToken ct);
}

public interface IVectorStore
{
    Task UpsertAsync(VectorUpsertRequest request, CancellationToken ct);
    Task<VectorSearchResult> SearchAsync(VectorSearchRequest request, CancellationToken ct);
    Task DeleteDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct);
}

public interface IRetriever
{
    Task<RetrievedContext> RetrieveAsync(RetrievalPlan request, CancellationToken ct);
}

public interface IReranker
{
    Task<IReadOnlyCollection<RetrievedChunk>> RerankAsync(RerankRequest request, CancellationToken ct);
}

public interface IPromptAssembler
{
    Task<PromptAssemblyResult> AssembleAsync(PromptAssemblyRequest request, CancellationToken ct);
}

public interface IWebSearchTool
{
    Task<WebSearchResult> SearchAsync(WebSearchRequest request, CancellationToken ct);
}

public interface IFileSearchTool
{
    Task<FileSearchResult> SearchAsync(FileSearchRequest request, CancellationToken ct);
}

public interface ICodeInterpreter
{
    Task<CodeInterpreterResult> ExecuteAsync(CodeInterpreterRequest request, CancellationToken ct);
}

public interface IAgentRuntime
{
    Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken ct);
}

public interface IOperationalAuditStore
{
    Task WriteRetrievalLogAsync(RetrievalLogRecord record, CancellationToken ct);
    Task WritePromptAssemblyAsync(PromptAssemblyRecord record, CancellationToken ct);
    Task WriteAgentRunAsync(AgentRunRecord record, CancellationToken ct);
    Task WriteToolExecutionAsync(ToolExecutionRecord record, CancellationToken ct);
    Task<IReadOnlyCollection<RetrievalLogRecord>> ReadRetrievalLogsAsync(Guid tenantId, int limit, CancellationToken ct);
    Task<IReadOnlyCollection<PromptAssemblyRecord>> ReadPromptAssembliesAsync(Guid tenantId, int limit, CancellationToken ct);
    Task<IReadOnlyCollection<AgentRunRecord>> ReadAgentRunsAsync(Guid tenantId, int limit, CancellationToken ct);
    Task<IReadOnlyCollection<ToolExecutionRecord>> ReadToolExecutionsAsync(Guid tenantId, int limit, CancellationToken ct);
    Task<OperationalAuditFeedResult> ReadAuditFeedAsync(OperationalAuditFeedQuery query, CancellationToken ct);
}

public sealed class OperationalAuditFeedQuery
{
    public Guid TenantId { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Cursor { get; set; }
    public int Limit { get; set; } = 20;
}

public sealed class OperationalAuditFeedResult
{
    public IReadOnlyCollection<OperationalAuditFeedItem> Entries { get; set; } = Array.Empty<OperationalAuditFeedItem>();
    public string? NextCursor { get; set; }
}

public sealed class OperationalAuditFeedItem
{
    public string EntryId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? DetailsJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class ContentSourceDescriptor
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public Stream Content { get; set; } = Stream.Null;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class ExtractedContentResult
{
    public string ExtractorName { get; set; } = string.Empty;
    public string ExtractorVersion { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public string? StructuredJson { get; set; }
    public List<StructuralSpan> Spans { get; set; } = new();
}

public sealed class StructuredExtractionResult
{
    public string ExtractorName { get; set; } = string.Empty;
    public string ExtractorVersion { get; set; } = string.Empty;
    public string StructuredJson { get; set; } = string.Empty;
    public string SemanticText { get; set; } = string.Empty;
}

public sealed class StructuralSpan
{
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public string? WorksheetName { get; set; }
    public int? SlideNumber { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class ChunkingRequest
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public ExtractedContentResult ExtractedContent { get; set; } = new();
    public StructuredExtractionResult? StructuredContent { get; set; }
    public int TokenBudget { get; set; }
}

public sealed class ChunkCandidate
{
    public string ChunkId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? EntitiesJson { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class EmbeddingBatchRequest
{
    public string EmbeddingModelName { get; set; } = string.Empty;
    public string EmbeddingModelVersion { get; set; } = string.Empty;
    public IReadOnlyCollection<EmbeddingInput> Inputs { get; set; } = Array.Empty<EmbeddingInput>();
}

public sealed class EmbeddingInput
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class EmbeddingEnvelope
{
    public string ChunkId { get; set; } = string.Empty;
    public string EmbeddingModelName { get; set; } = string.Empty;
    public string EmbeddingModelVersion { get; set; } = string.Empty;
    public int VectorDimensions { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
}

public sealed class VectorUpsertRequest
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public IReadOnlyCollection<VectorChunkRecord> Chunks { get; set; } = Array.Empty<VectorChunkRecord>();
}

public sealed class VectorChunkRecord
{
    public string ChunkId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public float[] Vector { get; set; } = Array.Empty<float>();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class VectorSearchRequest
{
    public Guid TenantId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public float[]? QueryVector { get; set; }
    public int TopK { get; set; }
    public double ScoreThreshold { get; set; }
    public Dictionary<string, string[]> Filters { get; set; } = new();
}

public sealed class VectorSearchResult
{
    public IReadOnlyCollection<RetrievedChunk> Chunks { get; set; } = Array.Empty<RetrievedChunk>();
    public string Strategy { get; set; } = string.Empty;
}

public sealed class RetrievalPlan
{
    public Guid TenantId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public bool UseHybridRetrieval { get; set; }
    public bool UseDenseRetrieval { get; set; } = true;
    public bool UseReranking { get; set; } = true;
    public int TopK { get; set; }
    public int MaxContextChunks { get; set; }
    public Dictionary<string, string[]> Filters { get; set; } = new();
}

public sealed class RetrievedContext
{
    public IReadOnlyCollection<RetrievedChunk> Chunks { get; set; } = Array.Empty<RetrievedChunk>();
    public string RetrievalStrategy { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
}

public sealed class RetrievedChunk
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public double Score { get; set; }
    public string Text { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class RerankRequest
{
    public Guid TenantId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public IReadOnlyCollection<RetrievedChunk> Candidates { get; set; } = Array.Empty<RetrievedChunk>();
    public int TopK { get; set; }
}

public sealed class PromptAssemblyRequest
{
    public Guid TenantId { get; set; }
    public string SystemInstructions { get; set; } = string.Empty;
    public string UserQuestion { get; set; } = string.Empty;
    public IReadOnlyCollection<RetrievedChunk> Chunks { get; set; } = Array.Empty<RetrievedChunk>();
    public int MaxPromptTokens { get; set; }
    public bool AllowGeneralKnowledge { get; set; }
}

public sealed class PromptAssemblyResult
{
    public string Prompt { get; set; } = string.Empty;
    public IReadOnlyCollection<string> IncludedChunkIds { get; set; } = Array.Empty<string>();
    public int EstimatedPromptTokens { get; set; }
    public IReadOnlyCollection<string> HumanReadableCitations { get; set; } = Array.Empty<string>();
}

public sealed class WebSearchRequest
{
    public Guid TenantId { get; set; }
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; }
}

public sealed class WebSearchResult
{
    public IReadOnlyCollection<WebSearchHit> Hits { get; set; } = Array.Empty<WebSearchHit>();
}

public sealed class WebSearchHit
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public double Score { get; set; }
}

public sealed class FileSearchRequest
{
    public Guid TenantId { get; set; }
    public string Query { get; set; } = string.Empty;
    public Dictionary<string, string[]> Filters { get; set; } = new();
    public int TopK { get; set; }
}

public sealed class FileSearchResult
{
    public IReadOnlyCollection<RetrievedChunk> Matches { get; set; } = Array.Empty<RetrievedChunk>();
}

public sealed class CodeInterpreterRequest
{
    public Guid TenantId { get; set; }
    public string Language { get; set; } = "python";
    public string Code { get; set; } = string.Empty;
    public IReadOnlyCollection<string> InputArtifacts { get; set; } = Array.Empty<string>();
}

public sealed class CodeInterpreterResult
{
    public int ExitCode { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
    public IReadOnlyCollection<string> OutputArtifacts { get; set; } = Array.Empty<string>();
}

public sealed class AgentRunRequest
{
    public Guid TenantId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public int ToolBudget { get; set; }
    public Dictionary<string, object?> Input { get; set; } = new();
}

public sealed class AgentRunResult
{
    public Guid AgentRunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object?> Output { get; set; } = new();
}