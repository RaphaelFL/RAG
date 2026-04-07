namespace Chatbot.Api.Middleware;

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