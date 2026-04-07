namespace Chatbot.Application.Abstractions;

public sealed class ChunkingRequest
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public ExtractedContentResult ExtractedContent { get; set; } = new();
    public StructuredExtractionResult? StructuredContent { get; set; }
    public int TokenBudget { get; set; }
}

public sealed class ChunkCandidate
{
    public string ChunkId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? EntitiesJson { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}