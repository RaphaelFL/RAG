namespace Chatbot.Domain.Entities;

public sealed class DocumentRecord
{
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string LogicalName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ParentDocumentHash { get; set; } = string.Empty;
    public string CurrentVersionHash { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string? AccessControlList { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
