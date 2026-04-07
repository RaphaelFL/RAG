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