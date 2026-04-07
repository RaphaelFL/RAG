namespace Chatbot.Application.Contracts;

public sealed class GenerateEmbeddingItemRequestDtoV2
{
    public string ChunkId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
