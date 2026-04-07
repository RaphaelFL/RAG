namespace Chatbot.Domain.Entities;

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
    public string Role { get; set; } = string.Empty;
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