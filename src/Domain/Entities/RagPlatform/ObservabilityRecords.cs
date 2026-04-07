namespace Chatbot.Domain.Entities;

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