using Chatbot.Application.Abstractions;

namespace Chatbot.Application.Services;

internal sealed class DocumentReindexAccessGuard : IDocumentReindexAccessGuard
{
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly IDocumentAuthorizationService _documentAuthorizationService;
    private readonly ISecurityAuditLogger _securityAuditLogger;

    public DocumentReindexAccessGuard(
        IRequestContextAccessor requestContextAccessor,
        IDocumentAuthorizationService documentAuthorizationService,
        ISecurityAuditLogger securityAuditLogger)
    {
        _requestContextAccessor = requestContextAccessor;
        _documentAuthorizationService = documentAuthorizationService;
        _securityAuditLogger = securityAuditLogger;
    }

    public void EnsureAccess(DocumentCatalogEntry document)
    {
        var canAccess = _requestContextAccessor.TenantId.HasValue && _documentAuthorizationService.CanAccess(
            document,
            _requestContextAccessor.TenantId,
            _requestContextAccessor.UserId,
            _requestContextAccessor.UserRole);

        if (!canAccess)
        {
            _securityAuditLogger.LogAccessDenied(_requestContextAccessor.UserId, $"document:{document.DocumentId}");
            throw new UnauthorizedAccessException("Document does not belong to the current tenant.");
        }
    }
}