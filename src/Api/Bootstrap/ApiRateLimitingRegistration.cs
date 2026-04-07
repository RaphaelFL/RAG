using System.Security.Claims;
using System.Threading.RateLimiting;
using Chatbot.Api.Contracts;

namespace Chatbot.Api.Bootstrap;

public static class ApiRateLimitingRegistration
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var traceId = context.HttpContext.TraceIdentifier;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString();
                }

                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(new ErrorResponseDto
                {
                    Code = "rate_limit_exceeded",
                    Message = "Rate limit exceeded",
                    TraceId = traceId
                }, cancellationToken);
            };

            options.AddPolicy("chat", context =>
                RateLimitPartition.GetFixedWindowLimiter(GetPartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

            options.AddPolicy("chat-stream", context =>
                RateLimitPartition.GetConcurrencyLimiter(GetPartitionKey(context), _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = 3,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }));

            options.AddPolicy("search", context =>
                RateLimitPartition.GetFixedWindowLimiter(GetPartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 500,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

            options.AddPolicy("upload", context =>
                RateLimitPartition.GetFixedWindowLimiter(GetPartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromDays(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

            options.AddPolicy("reindex", context =>
                RateLimitPartition.GetFixedWindowLimiter(GetPartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });

        return services;
    }

    private static string GetPartitionKey(HttpContext context)
    {
        var tenantId = context.User.FindFirstValue("tenant_id") ?? "anonymous";
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        return $"{tenantId}:{userId}";
    }
}