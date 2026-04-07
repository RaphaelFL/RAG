namespace Chatbot.Application.Contracts;

public class SearchQueryItemDto
{
    public Guid DocumentId { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public double Score { get; set; }
}
