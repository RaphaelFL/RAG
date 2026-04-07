namespace Chatbot.Application.Abstractions;

public interface IDocumentExtractionResultBuilder
{
    DocumentTextExtractionResultDto Build(string text, string strategy, string? provider, IReadOnlyCollection<PageExtractionDto>? rawPages, string? structuredJson);
}