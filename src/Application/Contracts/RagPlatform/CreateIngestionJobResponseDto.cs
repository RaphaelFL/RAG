namespace Chatbot.Application.Contracts;

public sealed class CreateIngestionJobResponseDto
{
    public Guid IngestionJobId { get; set; }
    public Guid DocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
}
