namespace Chatbot.Application.Contracts;

public sealed class RetrievalRequestDto
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 8;
    public bool UseHybridRetrieval { get; set; } = true;
    public bool UseReranking { get; set; } = true;
    public Dictionary<string, string[]> Filters { get; set; } = new();
}

public sealed class RetrievalResponseDto
{
    public string Strategy { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
    public List<RetrievedChunkDtoV2> Chunks { get; set; } = new();
}

public sealed class RetrievedChunkDtoV2
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}