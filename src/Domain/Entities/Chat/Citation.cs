namespace Chatbot.Domain.Entities;

public class Citation
{
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string ChunkId { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public Location? Location { get; set; }
    public double Score { get; set; }
}
