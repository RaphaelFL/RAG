namespace Chatbot.Application.Abstractions;

public interface IDocumentMetadataExtractionService
{
    Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct);
}