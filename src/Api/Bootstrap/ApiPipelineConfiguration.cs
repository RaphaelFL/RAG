using Chatbot.Api.Middleware;
using Microsoft.AspNetCore.HttpOverrides;

namespace Chatbot.Api.Bootstrap;

public static class ApiPipelineConfiguration
{
    public static WebApplication ConfigureApiPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        else
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseCors("ConfiguredOrigins");
        app.UseAuthentication();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RateLimitHeadersMiddleware>();
        app.UseRateLimiter();
        app.UseAuthorization();

        app.MapGet("/", () => Results.Ok(new
        {
            name = "Chatbot.Api",
            status = "running",
            swagger = "/swagger",
            health = "/health",
            timestampUtc = DateTime.UtcNow
        }))
            .AllowAnonymous();

        app.MapGet("/favicon.ico", () => Results.NoContent())
            .AllowAnonymous();

        app.MapControllers();

        app.MapGet("/api/v1/health", () => Results.Ok(new
        {
            status = "Healthy",
            dependencies = new
            {
                vectorStore = "Healthy",
                documentStorage = "Healthy",
                aiRuntime = "Healthy",
                ocr = "Healthy"
            },
            timestampUtc = DateTime.UtcNow
        }))
            .WithName("Health")
            .AllowAnonymous()
            .WithOpenApi();

        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
            .AllowAnonymous();

        return app;
    }
}