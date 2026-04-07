namespace Chatbot.Application.Abstractions;

public sealed class EmbeddingBatchRequest
{
    public string EmbeddingModelName { get; set; } = string.Empty;
    public string EmbeddingModelVersion { get; set; } = string.Empty;
    public IReadOnlyCollection<EmbeddingInput> Inputs { get; set; } = Array.Empty<EmbeddingInput>();
}
