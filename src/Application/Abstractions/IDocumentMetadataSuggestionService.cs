namespace Chatbot.Application.Abstractions;

public interface IDocumentMetadataSuggestionService
{
    Task<DocumentMetadataSuggestionDto> SuggestAsync(IngestDocumentCommand command, CancellationToken ct);
}