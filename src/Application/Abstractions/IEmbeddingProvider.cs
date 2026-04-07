namespace Chatbot.Application.Abstractions;

public interface IEmbeddingProvider
{
    Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct);
}