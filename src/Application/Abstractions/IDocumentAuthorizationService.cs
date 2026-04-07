namespace Chatbot.Application.Abstractions;

public interface IDocumentAuthorizationService
{
    bool CanAccess(DocumentCatalogEntry document, Guid? tenantId, string? userId, string? userRole);
}