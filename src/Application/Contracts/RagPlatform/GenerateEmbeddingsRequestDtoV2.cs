namespace Chatbot.Application.Contracts;

public sealed class GenerateEmbeddingsRequestDtoV2
{
    public string? EmbeddingModelName { get; set; }
    public string? EmbeddingModelVersion { get; set; }
    public List<GenerateEmbeddingItemRequestDtoV2> Items { get; set; } = new();
}
