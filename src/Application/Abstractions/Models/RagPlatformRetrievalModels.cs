namespace Chatbot.Application.Abstractions;

public sealed class VectorUpsertRequest
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public IReadOnlyCollection<VectorChunkRecord> Chunks { get; set; } = Array.Empty<VectorChunkRecord>();
}

public sealed class VectorChunkRecord
{
    public string ChunkId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public float[] Vector { get; set; } = Array.Empty<float>();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class VectorSearchRequest
{
    public Guid TenantId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public float[]? QueryVector { get; set; }
    public int TopK { get; set; }
    public double ScoreThreshold { get; set; }
    public Dictionary<string, string[]> Filters { get; set; } = new();
}

public sealed class VectorSearchResult
{
    public IReadOnlyCollection<RetrievedChunk> Chunks { get; set; } = Array.Empty<RetrievedChunk>();
    public string Strategy { get; set; } = string.Empty;
}

public sealed class RetrievalPlan
{
    public Guid TenantId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public bool UseHybridRetrieval { get; set; }
    public bool UseDenseRetrieval { get; set; } = true;
    public bool UseReranking { get; set; } = true;
    public int TopK { get; set; }
    public int MaxContextChunks { get; set; }
    public Dictionary<string, string[]> Filters { get; set; } = new();
}

public sealed class RetrievedContext
{
    public IReadOnlyCollection<RetrievedChunk> Chunks { get; set; } = Array.Empty<RetrievedChunk>();
    public string RetrievalStrategy { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
}

public sealed class RetrievedChunk
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public double Score { get; set; }
    public string Text { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class RerankRequest
{
    public Guid TenantId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public IReadOnlyCollection<RetrievedChunk> Candidates { get; set; } = Array.Empty<RetrievedChunk>();
    public int TopK { get; set; }
}