namespace Chatbot.Application.Abstractions;

public interface IDocumentParser
{
    bool CanParse(IngestDocumentCommand command);
    Task<DocumentParseResultDto?> ParseAsync(IngestDocumentCommand command, CancellationToken ct);
}