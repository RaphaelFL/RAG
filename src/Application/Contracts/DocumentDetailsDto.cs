namespace Chatbot.Application.Contracts;

public class DocumentDetailsDto
{
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public int IndexedChunkCount { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? Source { get; set; }
    public Guid? LastJobId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DocumentMetadataDto Metadata { get; set; } = new();
}
