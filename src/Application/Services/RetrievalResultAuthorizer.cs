using Chatbot.Application.Abstractions;

namespace Chatbot.Application.Services;

public sealed class RetrievalResultAuthorizer : IRetrievalResultAuthorizer
{
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IDocumentAuthorizationService _documentAuthorizationService;
    private readonly IRequestContextAccessor _requestContextAccessor;

    public RetrievalResultAuthorizer(
        IDocumentCatalog documentCatalog,
        IDocumentAuthorizationService documentAuthorizationService,
        IRequestContextAccessor requestContextAccessor)
    {
        _documentCatalog = documentCatalog;
        _documentAuthorizationService = documentAuthorizationService;
        _requestContextAccessor = requestContextAccessor;
    }

    public IReadOnlyList<SearchResultDto> Authorize(IReadOnlyCollection<SearchResultDto> results)
    {
        return results
            .Where(result => HasAccess(result.DocumentId))
            .ToList();
    }

    private bool HasAccess(Guid documentId)
    {
        var document = _documentCatalog.Get(documentId);
        return document is not null && _documentAuthorizationService.CanAccess(
            document,
            _requestContextAccessor.TenantId,
            _requestContextAccessor.UserId,
            _requestContextAccessor.UserRole);
    }
}