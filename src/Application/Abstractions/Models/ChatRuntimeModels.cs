namespace Chatbot.Application.Abstractions;

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