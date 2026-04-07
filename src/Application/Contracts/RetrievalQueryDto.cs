namespace Chatbot.Application.Contracts;

public class RetrievalQueryDto
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
    public List<Guid>? DocumentIds { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Categories { get; set; }
    public List<string>? ContentTypes { get; set; }
    public List<string>? Sources { get; set; }
    public bool SemanticRanking { get; set; } = true;
}
