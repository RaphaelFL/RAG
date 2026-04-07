namespace Chatbot.Application.Contracts;

public class RetrievalResultDto
{
    public List<RetrievedChunkDto> Chunks { get; set; } = new();
    public string RetrievalStrategy { get; set; } = "hybrid";
    public long LatencyMs { get; set; }
}
