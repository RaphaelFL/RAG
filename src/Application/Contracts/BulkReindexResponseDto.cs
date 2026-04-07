namespace Chatbot.Application.Contracts;

public class BulkReindexResponseDto
{
    public bool Accepted { get; set; }
    public Guid JobId { get; set; }
    public string Mode { get; set; } = "incremental";
    public int DocumentCount { get; set; }
}
