namespace Chatbot.Application.Abstractions;

public interface IDocumentIngestionService
{
    Task<UploadDocumentResponseDto> IngestAsync(IngestDocumentCommand command, CancellationToken ct);
}