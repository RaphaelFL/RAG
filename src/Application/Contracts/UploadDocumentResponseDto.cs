namespace Chatbot.Application.Contracts;

public class UploadDocumentResponseDto
{
    public Guid DocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid IngestionJobId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
