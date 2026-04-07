namespace Chatbot.Application.Contracts;

public class ChatRequestDto
{
    public Guid SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TemplateId { get; set; } = "grounded_answer";
    public string TemplateVersion { get; set; } = "1.0.0";
    public ChatFiltersDto? Filters { get; set; }
    public ChatOptionsDto? Options { get; set; }
}

public class ChatFiltersDto
{
    public List<Guid>? DocumentIds { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Categories { get; set; }
    public List<string>? ContentTypes { get; set; }
    public List<string>? Sources { get; set; }
}

public class ChatOptionsDto
{
    public int MaxCitations { get; set; } = 5;
    public bool AllowGeneralKnowledge { get; set; } = true;
    public bool SemanticRanking { get; set; } = true;
}

public class ChatResponseDto
{
    public Guid AnswerId { get; set; }
    public Guid SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<CitationDto> Citations { get; set; } = new();
    public UsageMetadataDto Usage { get; set; } = new();
    public ChatPolicyDto Policy { get; set; } = new();
    public DateTime TimestampUtc { get; set; }
}

public class CitationDto
{
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string ChunkId { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public LocationDto? Location { get; set; }
    public double Score { get; set; }
}

public class LocationDto
{
    public int? Page { get; set; }
    public int? EndPage { get; set; }
    public string? Section { get; set; }
}

public class UsageMetadataDto
{
    public string Model { get; set; } = "gpt-4.1";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public long LatencyMs { get; set; }
    public string RetrievalStrategy { get; set; } = "hybrid";
    public Dictionary<string, long> RuntimeMetrics { get; set; } = new();
}

public class ChatPolicyDto
{
    public bool Grounded { get; set; }
    public bool HadEnoughEvidence { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = "1.0.0";
}

public class StreamingChatEventDto
{
    public string EventType { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public class StreamStartedEventDto
{
    public Guid AnswerId { get; set; }
    public Guid SessionId { get; set; }
}

public class StreamDeltaEventDto
{
    public string Text { get; set; } = string.Empty;
}

public class StreamCompletedEventDto
{
    public UsageMetadataDto Usage { get; set; } = new();
}

public class StreamErrorEventDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
}