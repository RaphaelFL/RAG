namespace Chatbot.Application.Abstractions;

public interface IDocumentTextExtractor
{
    Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct);
}