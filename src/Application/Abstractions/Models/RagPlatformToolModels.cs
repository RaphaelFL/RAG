namespace Chatbot.Application.Abstractions;

public sealed class PromptAssemblyRequest
{
    public Guid TenantId { get; set; }
    public string SystemInstructions { get; set; } = string.Empty;
    public string UserQuestion { get; set; } = string.Empty;
    public IReadOnlyCollection<RetrievedChunk> Chunks { get; set; } = Array.Empty<RetrievedChunk>();
    public int MaxPromptTokens { get; set; }
    public bool AllowGeneralKnowledge { get; set; }
}

public sealed class PromptAssemblyResult
{
    public string Prompt { get; set; } = string.Empty;
    public IReadOnlyCollection<string> IncludedChunkIds { get; set; } = Array.Empty<string>();
    public int EstimatedPromptTokens { get; set; }
    public IReadOnlyCollection<string> HumanReadableCitations { get; set; } = Array.Empty<string>();
}

public sealed class WebSearchRequest
{
    public Guid TenantId { get; set; }
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; }
}

public sealed class WebSearchResult
{
    public IReadOnlyCollection<WebSearchHit> Hits { get; set; } = Array.Empty<WebSearchHit>();
}

public sealed class WebSearchHit
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public double Score { get; set; }
}

public sealed class FileSearchRequest
{
    public Guid TenantId { get; set; }
    public string Query { get; set; } = string.Empty;
    public Dictionary<string, string[]> Filters { get; set; } = new();
    public int TopK { get; set; }
}

public sealed class FileSearchResult
{
    public IReadOnlyCollection<RetrievedChunk> Matches { get; set; } = Array.Empty<RetrievedChunk>();
}

public sealed class CodeInterpreterRequest
{
    public Guid TenantId { get; set; }
    public string Language { get; set; } = "python";
    public string Code { get; set; } = string.Empty;
    public IReadOnlyCollection<string> InputArtifacts { get; set; } = Array.Empty<string>();
}

public sealed class CodeInterpreterResult
{
    public int ExitCode { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
    public IReadOnlyCollection<string> OutputArtifacts { get; set; } = Array.Empty<string>();
}

public sealed class AgentRunRequest
{
    public Guid TenantId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public int ToolBudget { get; set; }
    public Dictionary<string, object?> Input { get; set; } = new();
}

public sealed class AgentRunResult
{
    public Guid AgentRunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object?> Output { get; set; } = new();
}