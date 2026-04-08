namespace Chatbot.Application.Abstractions;

public interface IOriginalDocumentReader
{
    Task<DocumentFileReference?> GetOriginalDocumentAsync(Guid documentId, CancellationToken ct);
}