using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit.RetrievalServiceTestsSupport;

internal sealed class AllowAllDocumentAuthorizationService : IDocumentAuthorizationService
{
    public bool CanAccess(DocumentCatalogEntry document, Guid? tenantId, string? userId, string? userRole) => true;
}
