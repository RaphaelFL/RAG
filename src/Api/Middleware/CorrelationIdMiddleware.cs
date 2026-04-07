using System.Security.Claims;
using Chatbot.Application.Abstractions;

namespace Chatbot.Api.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public CorrelationIdMiddleware(RequestDelegate next, IRequestContextAccessor requestContextAccessor)
    {
        _next = next;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.ContainsKey(CorrelationIdHeader)
            ? context.Request.Headers[CorrelationIdHeader].ToString()
            : Guid.NewGuid().ToString();

        var tenantId = context.User.FindFirstValue("tenant_id");
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userRole = context.User.FindFirstValue(ClaimTypes.Role);

        context.Items["CorrelationId"] = correlationId;
        context.Items["TenantId"] = tenantId;
        context.Response.Headers.Append(CorrelationIdHeader, correlationId);

        _requestContextAccessor.TenantId = Guid.TryParse(tenantId, out var parsedTenantId)
            ? parsedTenantId
            : null;
        _requestContextAccessor.UserId = userId;
        _requestContextAccessor.UserRole = userRole;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TenantId", tenantId ?? "anonymous"))
        {
            await _next(context);
        }

        _requestContextAccessor.TenantId = null;
        _requestContextAccessor.UserId = null;
        _requestContextAccessor.UserRole = null;
    }
}