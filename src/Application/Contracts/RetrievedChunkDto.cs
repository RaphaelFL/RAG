namespace Chatbot.Application.Contracts;

public class RetrievedChunkDto
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
    public int PageNumber { get; set; }
    public int EndPageNumber { get; set; }
    public string? Section { get; set; }
}
