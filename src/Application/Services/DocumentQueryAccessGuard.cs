namespace Chatbot.Application.Services;

internal sealed class DocumentQueryAccessGuard
{
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly IDocumentAuthorizationService _documentAuthorizationService;
    private readonly ISecurityAuditLogger _securityAuditLogger;

    public DocumentQueryAccessGuard(
        IRequestContextAccessor requestContextAccessor,
        IDocumentAuthorizationService documentAuthorizationService,
        ISecurityAuditLogger securityAuditLogger)
    {
        _requestContextAccessor = requestContextAccessor;
        _documentAuthorizationService = documentAuthorizationService;
        _securityAuditLogger = securityAuditLogger;
    }

    public void EnsureTenantAccess(DocumentCatalogEntry document)
    {
        if (!HasTenantAccess(document))
        {
            _securityAuditLogger.LogAccessDenied(_requestContextAccessor.UserId, $"document:{document.DocumentId}");
            throw new UnauthorizedAccessException("Document does not belong to the current tenant.");
        }
    }

    public bool HasTenantAccess(DocumentCatalogEntry document)
    {
        return _requestContextAccessor.TenantId.HasValue && _documentAuthorizationService.CanAccess(
            document,
            _requestContextAccessor.TenantId,
            _requestContextAccessor.UserId,
            _requestContextAccessor.UserRole);
    }
}