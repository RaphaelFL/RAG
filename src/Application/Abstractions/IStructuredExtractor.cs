namespace Chatbot.Application.Abstractions;

public interface IStructuredExtractor
{
    bool CanHandle(ExtractedContentResult content);
    Task<StructuredExtractionResult> ExtractAsync(ExtractedContentResult content, CancellationToken ct);
}