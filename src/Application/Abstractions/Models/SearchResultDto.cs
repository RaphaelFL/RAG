namespace Chatbot.Application.Abstractions;

public class SearchResultDto
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
