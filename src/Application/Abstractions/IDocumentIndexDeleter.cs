namespace Chatbot.Application.Abstractions;

public interface IDocumentIndexDeleter
{
    Task DeleteDocumentAsync(Guid documentId, CancellationToken ct);
}