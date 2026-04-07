namespace Chatbot.Application.Abstractions;

public interface IContentExtractor
{
    bool CanHandle(ContentSourceDescriptor source);
    Task<ExtractedContentResult> ExtractAsync(ContentSourceDescriptor source, CancellationToken ct);
}