using Chatbot.Application.Abstractions;

namespace Backend.Unit.DocumentReindexServiceTestsSupport;

internal sealed class DenyAllDocumentAuthorizationService : IDocumentAuthorizationService
{
    public bool CanAccess(DocumentCatalogEntry document, Guid? tenantId, string? userId, string? userRole) => false;
}