using System.Security.Claims;
using System.Threading.RateLimiting;
using Chatbot.Api.Authentication;
using Chatbot.Api.Contracts;
using Chatbot.Api.Middleware;
using Chatbot.Application;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Ingestion;
using Chatbot.Mcp;
using Chatbot.Retrieval;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Chatbot.Application.Observability;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/chatbot-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Chatbot.Api")
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<ChatRequestValidator>();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var details = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value?.Errors.Select(error => error.ErrorMessage).ToArray() ?? Array.Empty<string>());

        return new BadRequestObjectResult(new ErrorResponseDto
        {
            Code = "validation_error",
            Message = "Validation failed",
            Details = details,
            TraceId = context.HttpContext.TraceIdentifier
        });
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Use qualquer bearer token não vazio no ambiente atual."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddOptions<ChatModelOptions>()
    .Bind(builder.Configuration.GetRequiredSection("ChatModelOptions"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "ChatModelOptions:Model e obrigatorio.")
    .Validate(options => options.MaxTokens > 0, "ChatModelOptions:MaxTokens deve ser maior que zero.")
    .Validate(options => options.TopP is >= 0 and <= 1, "ChatModelOptions:TopP deve estar entre 0 e 1.")
    .ValidateOnStart();
builder.Services.AddOptions<EmbeddingOptions>()
    .Bind(builder.Configuration.GetRequiredSection("EmbeddingOptions"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "EmbeddingOptions:Model e obrigatorio.")
    .Validate(options => options.Dimensions > 0, "EmbeddingOptions:Dimensions deve ser maior que zero.")
    .Validate(options => options.BatchSize > 0, "EmbeddingOptions:BatchSize deve ser maior que zero.")
    .ValidateOnStart();
builder.Services.AddOptions<SearchOptions>()
    .Bind(builder.Configuration.GetRequiredSection("SearchOptions"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.IndexName), "SearchOptions:IndexName e obrigatorio.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.SemanticConfigurationName), "SearchOptions:SemanticConfigurationName e obrigatorio.")
    .Validate(options => options.HybridSearchWeight is >= 0 and <= 1, "SearchOptions:HybridSearchWeight deve estar entre 0 e 1.")
    .Validate(options => options.TopK > 0, "SearchOptions:TopK deve ser maior que zero.")
    .ValidateOnStart();
builder.Services.AddOptions<BlobStorageOptions>()
    .Bind(builder.Configuration.GetRequiredSection("BlobStorageOptions"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ContainerName), "BlobStorageOptions:ContainerName e obrigatorio.")
    .ValidateOnStart();
builder.Services.AddOptions<OcrOptions>()
    .Bind(builder.Configuration.GetRequiredSection("OcrOptions"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.PrimaryProvider), "OcrOptions:PrimaryProvider e obrigatorio.")
    .Validate(options => !options.EnableFallback || !string.IsNullOrWhiteSpace(options.FallbackProvider), "OcrOptions:FallbackProvider e obrigatorio quando EnableFallback=true.")
    .Validate(options => !string.Equals(options.PrimaryProvider, "AzureDocumentIntelligence", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrWhiteSpace(options.AzureDocumentIntelligenceModelId), "OcrOptions:AzureDocumentIntelligenceModelId e obrigatorio quando PrimaryProvider=AzureDocumentIntelligence.")
    .ValidateOnStart();
builder.Services.AddOptions<PromptTemplateOptions>()
    .Bind(builder.Configuration.GetRequiredSection("PromptTemplateOptions"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.GroundedAnswerVersion), "PromptTemplateOptions:GroundedAnswerVersion e obrigatorio.")
    .Validate(options => options.DefaultTimeout > 0, "PromptTemplateOptions:DefaultTimeout deve ser maior que zero.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.InsufficientEvidenceMessage), "PromptTemplateOptions:InsufficientEvidenceMessage e obrigatorio.")
    .Validate(options => options.BlockedInputPatterns.Length > 0, "PromptTemplateOptions:BlockedInputPatterns deve conter ao menos uma entrada.")
    .ValidateOnStart();
builder.Services.AddOptions<FeatureFlagOptions>()
    .Bind(builder.Configuration.GetRequiredSection("FeatureFlagOptions"))
    .ValidateOnStart();
builder.Services.AddOptions<ExternalProviderClientOptions>()
    .Bind(builder.Configuration.GetRequiredSection("ExternalProviderClientOptions"))
    .Validate(options => options.TimeoutSeconds > 0, "ExternalProviderClientOptions:TimeoutSeconds deve ser maior que zero.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.AzureOpenAiApiVersion), "ExternalProviderClientOptions:AzureOpenAiApiVersion e obrigatorio.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.AzureSearchApiVersion), "ExternalProviderClientOptions:AzureSearchApiVersion e obrigatorio.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.AzureDocumentIntelligenceApiVersion), "ExternalProviderClientOptions:AzureDocumentIntelligenceApiVersion e obrigatorio.")
    .ValidateOnStart();

builder.Services
    .AddAuthentication("HeaderBearer")
    .AddScheme<AuthenticationSchemeOptions, HeaderBearerAuthenticationHandler>("HeaderBearer", _ => { });

builder.Services.AddSingleton<IRequestContextAccessor, RequestContextAccessor>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationMiddlewareResultHandler>();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("DocumentAdmin", policy =>
        policy.RequireRole("Analyst", "TenantAdmin", "PlatformAdmin"));

    options.AddPolicy("McpAccess", policy =>
        policy.RequireRole("McpClient", "Analyst", "TenantAdmin", "PlatformAdmin"));
});

builder.Services.AddRateLimiter(options =>
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

builder.Services
    .AddApplication()
    .AddInfrastructure()
    .AddIngestion()
    .AddRetrieval()
    .AddMcp();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Chatbot.Api"))
    .WithTracing(tracing => tracing
        .AddSource(ChatbotTelemetry.ActivitySourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(ChatbotTelemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowAll");

// Custom middleware
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<RateLimitHeadersMiddleware>();

app.UseAuthentication();
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

// Add health check endpoint
app.MapGet("/api/v1/health", () => Results.Ok(new
{
    status = "Healthy",
    dependencies = new
    {
        search = "Healthy",
        blobStorage = "Healthy",
        chatModel = "Healthy"
    },
    timestampUtc = DateTime.UtcNow
}))
    .WithName("Health")
    .AllowAnonymous()
    .WithOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .AllowAnonymous();

static string GetPartitionKey(HttpContext context)
{
    var tenantId = context.User.FindFirstValue("tenant_id")
        ?? context.Request.Headers["X-Tenant-Id"].ToString();
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? "anonymous";

    return $"{tenantId}:{userId}";
}

try
{
    Log.Information("Starting Chatbot API");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
