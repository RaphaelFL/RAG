namespace Chatbot.Domain.Entities;

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
