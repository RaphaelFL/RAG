using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Chatbot.Api.Authentication;
using Chatbot.Api.Contracts;
using Chatbot.Api.Documents;
using Chatbot.Api.Middleware;
using Chatbot.Application;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Infrastructure;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Ingestion;
using Chatbot.Mcp;
using Chatbot.Retrieval;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Chatbot.Application.Observability;

var builder = WebApplication.CreateBuilder(args);

AddOptionalLocalConfiguration(builder);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore.DataProtection", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("logs/chatbot-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Chatbot.Api")
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddSingleton<IDocumentUploadValidator, DocumentUploadValidator>();
builder.Services.AddSingleton<IDocumentUploadCommandFactory, DocumentUploadCommandFactory>();
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
        Description = builder.Environment.IsDevelopment()
            ? "Em desenvolvimento, aceite JWT valido ou o modo de headers de desenvolvimento com bearer token nao vazio."
            : "Informe um JWT valido emitido para esta API."
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
    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        var allowedOrigins = ResolveAllowedOrigins(builder.Environment, builder.Configuration);

        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
                    IsConfiguredOrigin(origin, allowedOrigins)
                    || IsLocalDevelopmentOrigin(origin))
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                .AllowAnyHeader();

            return;
        }

        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .AllowAnyHeader();
    });
});
builder.Services.AddOptions<CorsPolicyOptions>()
    .Bind(builder.Configuration.GetSection("Cors"))
    .Validate(options => options.AllowedOrigins.All(IsAbsoluteOrigin), "Cors:AllowedOrigins deve conter apenas origens absolutas validas.");
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("JWT"));

static void AddOptionalLocalConfiguration(WebApplicationBuilder builder)
{
    var environmentName = builder.Environment.EnvironmentName;

    foreach (var directory in EnumerateConfigurationDirectories(builder.Environment.ContentRootPath))
    {
        AddJsonFileIfExists(builder.Configuration, Path.Combine(directory, "appsettings.local.json"));
        AddJsonFileIfExists(builder.Configuration, Path.Combine(directory, $"appsettings.{environmentName}.local.json"));
        AddJsonFileIfExists(builder.Configuration, Path.Combine(directory, "secrets.json"));
    }
}

static IEnumerable<string> EnumerateConfigurationDirectories(string contentRootPath)
{
    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var current = new DirectoryInfo(contentRootPath);

    for (var depth = 0; current is not null && depth < 4; depth += 1)
    {
        if (visited.Add(current.FullName))
        {
            yield return current.FullName;
        }

        current = current.Parent;
    }
}

static void AddJsonFileIfExists(ConfigurationManager configuration, string path)
{
    if (!File.Exists(path))
    {
        return;
    }

    configuration.AddJsonFile(path, optional: true, reloadOnChange: true);
}

builder.Services.AddOptions<ChatModelOptions>()
    .Bind(builder.Configuration.GetRequiredSection("ChatModelOptions"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "ChatModelOptions:Model e obrigatorio.")
    .Validate(options => options.MaxTokens > 0, "ChatModelOptions:MaxTokens deve ser maior que zero.")
    .Validate(options => options.MaxPromptContextTokens > 0, "ChatModelOptions:MaxPromptContextTokens deve ser maior que zero.")
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
builder.Services.AddOptions<ChunkingOptions>()
    .Bind(builder.Configuration.GetSection("ChunkingOptions"))
    .Validate(options => options.DenseChunkSize > 0, "ChunkingOptions:DenseChunkSize deve ser maior que zero.")
    .Validate(options => options.NarrativeChunkSize > 0, "ChunkingOptions:NarrativeChunkSize deve ser maior que zero.")
    .Validate(options => options.DenseOverlap >= 0, "ChunkingOptions:DenseOverlap nao pode ser negativo.")
    .Validate(options => options.NarrativeOverlap >= 0, "ChunkingOptions:NarrativeOverlap nao pode ser negativo.")
    .Validate(options => options.MinimumChunkCharacters > 0, "ChunkingOptions:MinimumChunkCharacters deve ser maior que zero.")
    .ValidateOnStart();
builder.Services.AddOptions<RetrievalOptimizationOptions>()
    .Bind(builder.Configuration.GetSection("RetrievalOptimizationOptions"))
    .Validate(options => options.CandidateMultiplier > 0, "RetrievalOptimizationOptions:CandidateMultiplier deve ser maior que zero.")
    .Validate(options => options.MaxCandidateCount > 0, "RetrievalOptimizationOptions:MaxCandidateCount deve ser maior que zero.")
    .Validate(options => options.MaxContextChunks > 0, "RetrievalOptimizationOptions:MaxContextChunks deve ser maior que zero.")
    .Validate(options => options.MinimumRerankScore is >= 0 and <= 1.5, "RetrievalOptimizationOptions:MinimumRerankScore deve estar entre 0 e 1.5.")
    .ValidateOnStart();
builder.Services.AddOptions<BlobStorageOptions>()
    .Bind(builder.Configuration.GetRequiredSection("BlobStorageOptions"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ContainerName), "BlobStorageOptions:ContainerName e obrigatorio.")
    .ValidateOnStart();
builder.Services.AddOptions<OcrOptions>()
    .Bind(builder.Configuration.GetRequiredSection("OcrOptions"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.PrimaryProvider), "OcrOptions:PrimaryProvider e obrigatorio.")
    .Validate(options => !options.EnableFallback || !string.IsNullOrWhiteSpace(options.FallbackProvider), "OcrOptions:FallbackProvider e obrigatorio quando EnableFallback=true.")
    .Validate(options => options.MinimumDirectTextCharacters >= 0, "OcrOptions:MinimumDirectTextCharacters nao pode ser negativo.")
    .Validate(options => options.MinimumDirectTextCoverageRatio is >= 0 and <= 1, "OcrOptions:MinimumDirectTextCoverageRatio deve estar entre 0 e 1.")
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
builder.Services.AddOptions<OperationalResilienceOptions>()
    .Bind(builder.Configuration.GetSection("OperationalResilienceOptions"))
    .Validate(options => options.TimeoutSeconds > 0, "OperationalResilienceOptions:TimeoutSeconds deve ser maior que zero.")
    .ValidateOnStart();
builder.Services.AddOptions<ProviderExecutionModeOptions>()
    .Bind(builder.Configuration.GetSection("ProviderExecutionModeOptions"))
    .Validate(options => !options.PreferMockProviders || options.AllowMockProviders, "ProviderExecutionModeOptions:PreferMockProviders exige AllowMockProviders=true.")
    .Validate(options => !options.PreferInMemoryInfrastructure || options.AllowInMemoryInfrastructure, "ProviderExecutionModeOptions:PreferInMemoryInfrastructure exige AllowInMemoryInfrastructure=true.")
    .Validate(options => !options.PreferLocalPersistentInfrastructure || options.AllowInMemoryInfrastructure, "ProviderExecutionModeOptions:PreferLocalPersistentInfrastructure exige AllowInMemoryInfrastructure=true.")
    .ValidateOnStart();
builder.Services.AddOptions<LocalPersistenceOptions>()
    .Bind(builder.Configuration.GetSection("LocalPersistenceOptions"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.BasePath), "LocalPersistenceOptions:BasePath e obrigatorio.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.BlobRootDirectory), "LocalPersistenceOptions:BlobRootDirectory e obrigatorio.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.DocumentCatalogFileName), "LocalPersistenceOptions:DocumentCatalogFileName e obrigatorio.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.SearchIndexFileName), "LocalPersistenceOptions:SearchIndexFileName e obrigatorio.")
    .ValidateOnStart();
builder.Services.AddOptions<CacheOptions>()
    .Bind(builder.Configuration.GetSection("CacheOptions"))
    .Validate(options => options.RetrievalTtlSeconds > 0, "CacheOptions:RetrievalTtlSeconds deve ser maior que zero.")
    .Validate(options => options.ChatCompletionTtlSeconds > 0, "CacheOptions:ChatCompletionTtlSeconds deve ser maior que zero.")
    .Validate(options => options.EmbeddingTtlHours > 0, "CacheOptions:EmbeddingTtlHours deve ser maior que zero.")
    .Validate(options => options.MaxInMemoryEntries > 0, "CacheOptions:MaxInMemoryEntries deve ser maior que zero.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.InstancePrefix), "CacheOptions:InstancePrefix e obrigatorio.")
    .ValidateOnStart();
builder.Services.AddOptions<RedisSettings>()
    .Bind(builder.Configuration.GetSection("RedisSettings"))
    .Validate(options => options.Port > 0, "RedisSettings:Port deve ser maior que zero.")
    .ValidateOnStart();
builder.Services.AddOptions<ExternalProviderClientOptions>()
    .Bind(builder.Configuration.GetRequiredSection("ExternalProviderClientOptions"))
    .Validate(options => options.TimeoutSeconds > 0, "ExternalProviderClientOptions:TimeoutSeconds deve ser maior que zero.")
    .ValidateOnStart();
builder.Services.AddOptions<Chatbot.Application.Configuration.EmbeddingGenerationOptions>()
    .Bind(builder.Configuration.GetSection("EmbeddingGenerationOptions"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ModelName), "EmbeddingGenerationOptions:ModelName e obrigatorio.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ModelVersion), "EmbeddingGenerationOptions:ModelVersion e obrigatorio.")
    .Validate(options => options.BatchSize > 0, "EmbeddingGenerationOptions:BatchSize deve ser maior que zero.")
    .Validate(options => options.Dimensions > 0, "EmbeddingGenerationOptions:Dimensions deve ser maior que zero.")
    .ValidateOnStart();
builder.Services.AddOptions<Chatbot.Application.Configuration.VectorStoreOptions>()
    .Bind(builder.Configuration.GetSection("VectorStoreOptions"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Provider), "VectorStoreOptions:Provider e obrigatorio.")
    .Validate(options => options.DefaultTopK > 0, "VectorStoreOptions:DefaultTopK deve ser maior que zero.")
    .ValidateOnStart();
builder.Services.AddOptions<Chatbot.Application.Configuration.RedisCoordinationOptions>()
    .Bind(builder.Configuration.GetSection("RedisCoordinationOptions"))
    .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.KeyPrefix), "RedisCoordinationOptions:KeyPrefix e obrigatorio quando habilitado.")
    .ValidateOnStart();
builder.Services.AddOptions<Chatbot.Application.Configuration.AgentRuntimeOptions>()
    .Bind(builder.Configuration.GetSection("AgentRuntimeOptions"))
    .Validate(options => options.MaxToolBudget > 0, "AgentRuntimeOptions:MaxToolBudget deve ser maior que zero.")
    .Validate(options => options.MaxDepth > 0, "AgentRuntimeOptions:MaxDepth deve ser maior que zero.")
    .ValidateOnStart();
builder.Services.AddOptions<Chatbot.Application.Configuration.CodeInterpreterOptions>()
    .Bind(builder.Configuration.GetSection("CodeInterpreterOptions"))
    .Validate(options => options.TimeoutSeconds > 0, "CodeInterpreterOptions:TimeoutSeconds deve ser maior que zero.")
    .Validate(options => options.MemoryLimitMb > 0, "CodeInterpreterOptions:MemoryLimitMb deve ser maior que zero.")
    .ValidateOnStart();
builder.Services.AddOptions<Chatbot.Application.Configuration.WebSearchOptions>()
    .Bind(builder.Configuration.GetSection("WebSearchOptions"))
    .Validate(options => options.DefaultTopK > 0, "WebSearchOptions:DefaultTopK deve ser maior que zero.")
    .Validate(options => options.TimeoutSeconds > 0, "WebSearchOptions:TimeoutSeconds deve ser maior que zero.")
    .ValidateOnStart();
builder.Services.AddOptions<Chatbot.Application.Configuration.StructuredExtractionOptions>()
    .Bind(builder.Configuration.GetSection("StructuredExtractionOptions"))
    .ValidateOnStart();

builder.Services
    .AddAuthentication("SmartAuth")
    .AddPolicyScheme("SmartAuth", "JWT or development header auth", options =>
    {
        options.ForwardDefaultSelector = context =>
            builder.Environment.IsDevelopment() && IsDevelopmentHeaderRequest(context.Request)
                ? "DevHeaderBearer"
                : JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        var jwtOptions = builder.Configuration.GetSection("JWT").Get<JwtOptions>() ?? new JwtOptions();
        var signingKey = ResolveSigningKey(jwtOptions)
            ?? throw new InvalidOperationException("JWT signing key not configured.");

        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(jwtOptions.Issuer),
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(jwtOptions.Audience),
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var identity = context.Principal?.Identity as ClaimsIdentity;
                if (identity is null)
                {
                    context.Fail("JWT identity ausente.");
                    return Task.CompletedTask;
                }

                var tenantClaim = identity.FindFirst("tenant_id")?.Value ?? identity.FindFirst("tenantId")?.Value;
                if (!Guid.TryParse(tenantClaim, out _))
                {
                    context.Fail("JWT precisa conter claim tenant_id valida.");
                    return Task.CompletedTask;
                }

                if (identity.FindFirst("tenant_id") is null)
                {
                    identity.AddClaim(new Claim("tenant_id", tenantClaim));
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                context.HttpContext.RequestServices
                    .GetRequiredService<ISecurityAuditLogger>()
                    .LogAuthenticationFailure(null, $"JWT validation failed: {context.Exception.Message}");
                return Task.CompletedTask;
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, HeaderBearerAuthenticationHandler>("DevHeaderBearer", _ => { });

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

// Add health check endpoint
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

static string GetPartitionKey(HttpContext context)
{
    var tenantId = context.User.FindFirstValue("tenant_id") ?? "anonymous";
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? "anonymous";

    return $"{tenantId}:{userId}";
}

static string[] ResolveAllowedOrigins(IHostEnvironment environment, IConfiguration configuration)
{
    var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?.Where(origin => IsAbsoluteOrigin(origin))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (configuredOrigins is { Length: > 0 })
    {
        return configuredOrigins;
    }

    if (environment.IsDevelopment())
    {
        return new[]
        {
            "http://localhost:3000",
            "http://localhost:3001",
            "https://localhost:3000",
            "https://localhost:3001",
            "http://localhost:15213",
            "https://localhost:15213",
            "http://localhost:15214",
            "https://localhost:15214"
        }
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    }

    throw new InvalidOperationException("Cors:AllowedOrigins deve ser configurado fora de desenvolvimento.");
}

static bool IsAbsoluteOrigin(string? origin)
{
    return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrWhiteSpace(uri.Host);
}

static bool IsConfiguredOrigin(string? origin, IEnumerable<string> allowedOrigins)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    var normalizedOrigin = uri.GetLeftPart(UriPartial.Authority);
    return allowedOrigins.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase);
}

static bool IsLocalDevelopmentOrigin(string? origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    var isHttp = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    var isLoopback = uri.IsLoopback
        || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);

    return isHttp && isLoopback && uri.Port > 0;
}

static bool IsDevelopmentHeaderRequest(HttpRequest request)
{
    return request.Headers.ContainsKey("X-Tenant-Id")
        || request.Headers.ContainsKey("X-User-Id")
        || request.Headers.ContainsKey("X-User-Role");
}

static SecurityKey? ResolveSigningKey(JwtOptions jwtOptions)
{
    if (!string.IsNullOrWhiteSpace(jwtOptions.SecKey))
    {
        var bytes = jwtOptions.SecKey
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => byte.TryParse(value, out var parsed) ? parsed : (byte)0)
            .ToArray();

        if (bytes.Length > 0)
        {
            return new SymmetricSecurityKey(bytes);
        }
    }

    if (string.IsNullOrWhiteSpace(jwtOptions.Key))
    {
        return null;
    }

    return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));
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
