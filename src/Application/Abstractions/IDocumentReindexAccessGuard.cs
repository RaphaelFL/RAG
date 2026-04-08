namespace Chatbot.Application.Abstractions;

public interface IDocumentReindexAccessGuard
{
    void EnsureAccess(DocumentCatalogEntry document);
}