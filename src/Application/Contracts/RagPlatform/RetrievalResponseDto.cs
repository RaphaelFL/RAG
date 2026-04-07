namespace Chatbot.Application.Contracts;

public sealed class RetrievalResponseDto
{
    public string Strategy { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
    public List<RetrievedChunkDtoV2> Chunks { get; set; } = new();
}
