namespace Chatbot.Application.Abstractions;

public interface IOcrProvider
{
    Task<OcrResultDto> ExtractAsync(Stream content, string fileName, CancellationToken ct);
    string ProviderName { get; }
}