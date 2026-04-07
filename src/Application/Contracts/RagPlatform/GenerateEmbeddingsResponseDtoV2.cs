namespace Chatbot.Application.Contracts;

public sealed class GenerateEmbeddingsResponseDtoV2
{
    public string ModelName { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public List<GenerateEmbeddingItemResponseDtoV2> Items { get; set; } = new();
}
