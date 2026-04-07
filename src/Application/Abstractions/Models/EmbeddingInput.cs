namespace Chatbot.Application.Abstractions;

public sealed class EmbeddingInput
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
