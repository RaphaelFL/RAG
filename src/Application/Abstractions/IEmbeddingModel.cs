namespace Chatbot.Application.Abstractions;

public interface IEmbeddingModel
{
    string ModelName { get; }
    string ModelVersion { get; }
    int Dimensions { get; }
    Task<IReadOnlyCollection<float[]>> GenerateAsync(IReadOnlyCollection<string> texts, CancellationToken ct);
}