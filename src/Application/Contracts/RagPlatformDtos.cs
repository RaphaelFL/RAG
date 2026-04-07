namespace Chatbot.Application.Contracts;

public sealed class CreateIngestionJobRequestDto
{
    public Guid TenantId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string? AccessControlList { get; set; }
    public List<string> Tags { get; set; } = new();
}

public sealed class CreateIngestionJobResponseDto
{
    public Guid IngestionJobId { get; set; }
    public Guid DocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class RetrievalRequestDto
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 8;
    public bool UseHybridRetrieval { get; set; } = true;
    public bool UseReranking { get; set; } = true;
    public Dictionary<string, string[]> Filters { get; set; } = new();
}

public sealed class RetrievalResponseDto
{
    public string Strategy { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
    public List<RetrievedChunkDtoV2> Chunks { get; set; } = new();
}

public sealed class RetrievedChunkDtoV2
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class PromptAssemblyRequestDto
{
    public string Question { get; set; } = string.Empty;
    public string SystemInstructions { get; set; } = string.Empty;
    public int MaxPromptTokens { get; set; } = 4000;
    public bool AllowGeneralKnowledge { get; set; }
    public List<RetrievedChunkDtoV2> Chunks { get; set; } = new();
}

public sealed class PromptAssemblyResponseDto
{
    public string Prompt { get; set; } = string.Empty;
    public int EstimatedPromptTokens { get; set; }
    public List<string> IncludedChunkIds { get; set; } = new();
    public List<string> Citations { get; set; } = new();
}

public sealed class AgentRunRequestDtoV2
{
    public string AgentName { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public int ToolBudget { get; set; } = 5;
    public Dictionary<string, object?> Input { get; set; } = new();
}

public sealed class AgentRunResponseDtoV2
{
    public Guid AgentRunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object?> Output { get; set; } = new();
}

public sealed class WebSearchRequestDtoV2
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
}

public sealed class WebSearchResponseDtoV2
{
    public List<WebSearchHitDtoV2> Hits { get; set; } = new();
}

public sealed class WebSearchHitDtoV2
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public double Score { get; set; }
}

public sealed class CodeInterpreterRequestDtoV2
{
    public string Language { get; set; } = "python";
    public string Code { get; set; } = string.Empty;
    public List<string> InputArtifacts { get; set; } = new();
}

public sealed class CodeInterpreterResponseDtoV2
{
    public int ExitCode { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
    public List<string> OutputArtifacts { get; set; } = new();
}

public sealed class GenerateEmbeddingsRequestDtoV2
{
    public string? EmbeddingModelName { get; set; }
    public string? EmbeddingModelVersion { get; set; }
    public List<GenerateEmbeddingItemRequestDtoV2> Items { get; set; } = new();
}

public sealed class GenerateEmbeddingItemRequestDtoV2
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class GenerateEmbeddingsResponseDtoV2
{
    public string ModelName { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public List<GenerateEmbeddingItemResponseDtoV2> Items { get; set; } = new();
}

public sealed class GenerateEmbeddingItemResponseDtoV2
{
    public string ChunkId { get; set; } = string.Empty;
    public int VectorDimensions { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
}

public sealed class OperationalAuditFeedResponseDto
{
    public List<OperationalAuditEntryDto> Entries { get; set; } = new();
    public string? NextCursor { get; set; }
}

public sealed class OperationalAuditEntryDto
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