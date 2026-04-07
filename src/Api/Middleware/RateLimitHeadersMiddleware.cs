namespace Chatbot.Api.Middleware;

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

        if (path.Equals("/api/v1/documents/ingest", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/documents/upload", StringComparison.OrdinalIgnoreCase))
        {
            SetFixedWindowHeaders(context, "upload", 10, TimeSpan.FromDays(1));
            return;
        }

        if (path.Equals("/api/v1/documents/reindex", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/reindex", StringComparison.OrdinalIgnoreCase))
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