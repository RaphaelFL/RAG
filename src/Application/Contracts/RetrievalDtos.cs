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

public class RetrievalResultDto
{
    public List<RetrievedChunkDto> Chunks { get; set; } = new();
    public string RetrievalStrategy { get; set; } = "hybrid";
    public long LatencyMs { get; set; }
}

public class SearchQueryRequestDto
{
    public string Query { get; set; } = string.Empty;
    public SearchFiltersDto? Filters { get; set; }
    public int Top { get; set; } = 5;
    public bool SemanticRanking { get; set; } = true;
}

public class SearchFiltersDto
{
    public List<Guid>? DocumentIds { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Categories { get; set; }
    public List<string>? ContentTypes { get; set; }
    public List<string>? Sources { get; set; }
}

public class SearchQueryResponseDto
{
    public List<SearchQueryItemDto> Items { get; set; } = new();
    public int Count { get; set; }
}

public class SearchQueryItemDto
{
    public Guid DocumentId { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public double Score { get; set; }
}

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