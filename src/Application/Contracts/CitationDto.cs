namespace Chatbot.Application.Contracts;

public class CitationDto
{
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string ChunkId { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public LocationDto? Location { get; set; }
    public double Score { get; set; }
}
