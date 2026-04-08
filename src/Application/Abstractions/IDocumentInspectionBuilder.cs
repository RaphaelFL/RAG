namespace Chatbot.Application.Abstractions;

public interface IDocumentInspectionBuilder
{
    DocumentInspectionDto Build(
        DocumentCatalogEntry document,
        IReadOnlyCollection<DocumentChunkIndexDto> chunks,
        string? search,
        int pageNumber,
        int pageSize);
}