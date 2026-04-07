namespace Chatbot.Domain.Entities;

public static class DocumentStatuses
{
    public const string Uploaded = "Uploaded";
    public const string Queued = "Queued";
    public const string Parsing = "Parsing";
    public const string OcrProcessing = "OcrProcessing";
    public const string Chunking = "Chunking";
    public const string Embedding = "Embedding";
    public const string Indexing = "Indexing";
    public const string Indexed = "Indexed";
    public const string ReindexPending = "ReindexPending";
    public const string Failed = "Failed";
    public const string Archived = "Archived";
    public const string Deleted = "Deleted";
}

public class ChatSession
{
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public List<ChatMessage> Messages { get; set; } = new();
}

public class ChatMessage
{
    public Guid MessageId { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty; // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = new();
    public UsageMetadata? Usage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class Citation
{
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string ChunkId { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public Location? Location { get; set; }
    public double Score { get; set; }
}

public class Location
{
    public int? Page { get; set; }
    public int? EndPage { get; set; }
    public string? Section { get; set; }
}

public class UsageMetadata
{
    public string Model { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public long LatencyMs { get; set; }
    public string RetrievalStrategy { get; set; } = "hybrid";
}

public class Document
{
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty; // "pdf", "docx", "txt", etc
    public string Status { get; set; } = DocumentStatuses.Uploaded; // Uploaded, Queued, Parsing, OcrProcessing, Chunking, Embedding, Indexing, Indexed, Failed
    public string StoragePath { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public DateTime? IndexedAtUtc { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Categories { get; set; } = new();
}

public class DocumentChunk
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public int PageNumber { get; set; }
    public string? Section { get; set; }
    public int Offset { get; set; }
    public int Length { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public enum UserRole
{
    TenantUser,
    Analyst,
    TenantAdmin,
    PlatformAdmin,
    BackgroundProcessor,
    McpClient
}
