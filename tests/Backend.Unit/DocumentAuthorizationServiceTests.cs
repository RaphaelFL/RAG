using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using FluentAssertions;
using Xunit;

namespace Backend.Unit;

public class DocumentAuthorizationServiceTests
{
    private readonly IDocumentAuthorizationService _sut = new DocumentAuthorizationService();

    [Fact]
    public void CanAccess_ShouldAllowSameTenant_WhenNoAccessPolicyExists()
    {
        var document = new DocumentCatalogEntry
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        };

        var result = _sut.CanAccess(document, document.TenantId, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "TenantUser");

        result.Should().BeTrue();
    }

    [Fact]
    public void CanAccess_ShouldDenySameTenant_WhenRoleIsNotAllowedByPolicy()
    {
        var document = new DocumentCatalogEntry
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AccessPolicy = "{\"allowedRoles\":[\"Analyst\"]}"
        };

        var result = _sut.CanAccess(document, document.TenantId, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "TenantUser");

        result.Should().BeFalse();
    }

    [Fact]
    public void CanAccess_ShouldAllowPlatformAdminCrossTenant_OnlyWhenPolicyExplicitlyAllows()
    {
        var document = new DocumentCatalogEntry
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AccessPolicy = "{\"allowPlatformAdminCrossTenant\":true}"
        };

        var result = _sut.CanAccess(document, Guid.Parse("22222222-2222-2222-2222-222222222222"), "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "PlatformAdmin");

        result.Should().BeTrue();
    }
}