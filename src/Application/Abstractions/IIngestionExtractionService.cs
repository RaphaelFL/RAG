namespace Chatbot.Application.Abstractions;

public interface IIngestionExtractionService
{
    Task<DocumentTextExtractionResultDto> ExtractAsync(Guid documentId, IngestDocumentCommand command, CancellationToken ct);
}