namespace Chatbot.Application.Abstractions;

public interface IDocumentQueryAccessGuard
{
    void EnsureTenantAccess(DocumentCatalogEntry document);

    bool HasTenantAccess(DocumentCatalogEntry document);
}