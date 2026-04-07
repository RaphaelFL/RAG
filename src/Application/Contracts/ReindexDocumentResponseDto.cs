namespace Chatbot.Application.Contracts;

public class ReindexDocumentResponseDto
{
    public Guid DocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ChunksReindexed { get; set; }
    public Guid? JobId { get; set; }
}
