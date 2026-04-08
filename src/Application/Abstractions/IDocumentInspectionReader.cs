namespace Chatbot.Application.Abstractions;

public interface IDocumentInspectionReader
{
    Task<DocumentInspectionDto?> GetDocumentInspectionAsync(Guid documentId, string? search, int pageNumber, int pageSize, CancellationToken ct);
}