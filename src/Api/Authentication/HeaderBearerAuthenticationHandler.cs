using System.Security.Claims;
using System.Text.Encodings.Web;
using Chatbot.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Chatbot.Api.Authentication;

public sealed class HeaderBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ISecurityAuditLogger _securityAuditLogger;

    public HeaderBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISecurityAuditLogger securityAuditLogger)
        : base(options, logger, encoder)
    {
        _securityAuditLogger = securityAuditLogger;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            _securityAuditLogger.LogAuthenticationFailure(null, "Missing Authorization header.");
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header."));
        }

        var authorizationValue = authorizationHeader.ToString();
        if (!authorizationValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _securityAuditLogger.LogAuthenticationFailure(null, "Authorization header must use Bearer scheme.");
            return Task.FromResult(AuthenticateResult.Fail("Authorization header must use Bearer scheme."));
        }

        var token = authorizationValue["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            _securityAuditLogger.LogAuthenticationFailure(null, "Bearer token cannot be empty.");
            return Task.FromResult(AuthenticateResult.Fail("Bearer token cannot be empty."));
        }

        if (!Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader))
        {
            _securityAuditLogger.LogAuthenticationFailure(null, "Missing X-Tenant-Id header.");
            return Task.FromResult(AuthenticateResult.Fail("Missing X-Tenant-Id header."));
        }

        if (!Guid.TryParse(tenantHeader.ToString(), out var tenantId))
        {
            _securityAuditLogger.LogAuthenticationFailure(null, "X-Tenant-Id must be a valid GUID.");
            return Task.FromResult(AuthenticateResult.Fail("X-Tenant-Id must be a valid GUID."));
        }

        var userId = Request.Headers.TryGetValue("X-User-Id", out var userHeader) && Guid.TryParse(userHeader.ToString(), out var parsedUserId)
            ? parsedUserId.ToString()
            : "00000000-0000-0000-0000-000000000001";

        var role = Request.Headers.TryGetValue("X-User-Role", out var roleHeader) && !string.IsNullOrWhiteSpace(roleHeader)
            ? roleHeader.ToString()
            : "TenantUser";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId),
            new(ClaimTypes.Role, role),
            new("tenant_id", tenantId.ToString())
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        return Response.WriteAsJsonAsync(new
        {
            code = "unauthorized",
            message = "Authentication is required",
            traceId = Context.TraceIdentifier
        });
    }
}
