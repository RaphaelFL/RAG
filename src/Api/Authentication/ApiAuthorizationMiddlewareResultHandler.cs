using Chatbot.Application.Contracts;
using Chatbot.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Chatbot.Api.Authentication;

public sealed class ApiAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    private readonly ISecurityAuditLogger _securityAuditLogger;

    public ApiAuthorizationMiddlewareResultHandler(ISecurityAuditLogger securityAuditLogger)
    {
        _securityAuditLogger = securityAuditLogger;
    }

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        context.Response.ContentType = "application/json";

        if (authorizeResult.Challenged)
        {
            _securityAuditLogger.LogAuthenticationFailure(context.User.Identity?.Name, "Authorization middleware challenge.");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ErrorResponseDto
            {
                Code = "unauthorized",
                Message = "Authentication is required.",
                TraceId = context.TraceIdentifier
            });
            return;
        }

        if (authorizeResult.Forbidden)
        {
            _securityAuditLogger.LogAccessDenied(context.User.Identity?.Name, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ErrorResponseDto
            {
                Code = "access_denied",
                Message = "You do not have permission to perform this operation.",
                TraceId = context.TraceIdentifier
            });
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}