namespace Chatbot.Api.Middleware;

using System.Security.Claims;
using Chatbot.Application.Abstractions;

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

        var tenantId = context.User.FindFirstValue("tenant_id")
            ?? context.Request.Headers["X-Tenant-Id"].ToString();
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userRole = context.User.FindFirstValue(ClaimTypes.Role)
            ?? context.Request.Headers["X-User-Role"].ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Items["TenantId"] = tenantId;
        context.Response.Headers.Append(CorrelationIdHeader, correlationId);

        _requestContextAccessor.TenantId = Guid.TryParse(tenantId, out var parsedTenantId)
            ? parsedTenantId
            : null;
        _requestContextAccessor.UserId = userId;
        _requestContextAccessor.UserRole = userRole;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TenantId", tenantId))
        {
            await _next(context);
        }

        _requestContextAccessor.TenantId = null;
        _requestContextAccessor.UserId = null;
        _requestContextAccessor.UserRole = null;
    }
}

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var correlationId = context.Items.TryGetValue("CorrelationId", out var corrId) 
            ? corrId?.ToString() ?? context.TraceIdentifier
            : context.TraceIdentifier;

        var statusCode = exception switch
        {
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        var code = exception switch
        {
            UnauthorizedAccessException => "access_denied",
            InvalidOperationException => "invalid_operation",
            _ => "internal_error"
        };

        var response = new
        {
            code,
            message = statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred"
                : exception.Message,
            traceId = correlationId
        };

        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(response);
    }
}

public class RateLimitHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public RateLimitHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ApplyHeaders(context);
        await _next(context);
    }

    private static void ApplyHeaders(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (path.Equals("/api/v1/chat/message", StringComparison.OrdinalIgnoreCase))
        {
            SetFixedWindowHeaders(context, "chat", 100, TimeSpan.FromMinutes(1));
            return;
        }

        if (path.Equals("/api/v1/chat/stream", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers["X-RateLimit-Policy"] = "chat-stream";
            context.Response.Headers["X-RateLimit-ConcurrentLimit"] = "3";
            return;
        }

        if (path.StartsWith("/api/v1/search", StringComparison.OrdinalIgnoreCase))
        {
            SetFixedWindowHeaders(context, "search", 500, TimeSpan.FromMinutes(1));
            return;
        }

        if (path.Equals("/api/v1/documents/ingest", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/v1/documents/upload", StringComparison.OrdinalIgnoreCase))
        {
            SetFixedWindowHeaders(context, "upload", 10, TimeSpan.FromDays(1));
            return;
        }

        if (path.Equals("/api/v1/documents/reindex", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/reindex", StringComparison.OrdinalIgnoreCase))
        {
            SetFixedWindowHeaders(context, "reindex", 20, TimeSpan.FromHours(1));
        }
    }

    private static void SetFixedWindowHeaders(HttpContext context, string policy, int limit, TimeSpan window)
    {
        context.Response.Headers["X-RateLimit-Policy"] = policy;
        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Window-Seconds"] = ((int)window.TotalSeconds).ToString();
    }
}
