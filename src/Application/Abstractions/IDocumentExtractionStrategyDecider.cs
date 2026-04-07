namespace Chatbot.Application.Abstractions;

public interface IDocumentExtractionStrategyDecider
{
    bool ShouldUseDirectText(IngestDocumentCommand command, string? parsedText);
    bool ShouldAttemptOcr(IngestDocumentCommand command, string? parsedText);
    bool ShouldRecordOcrAvoided(IngestDocumentCommand command);
}