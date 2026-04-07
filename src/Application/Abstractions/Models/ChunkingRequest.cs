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
