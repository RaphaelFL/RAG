namespace Chatbot.Domain.Entities;

public sealed class DocumentVersionRecord
{
    public Guid DocumentVersionId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public int VersionNumber { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string ParentDocumentHash { get; set; } = string.Empty;
    public string StorageUri { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
