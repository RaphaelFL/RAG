using Chatbot.Api.Contracts;
using Chatbot.Api.Documents;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Bootstrap;

public static class ApiPresentationRegistration
{
    public static IServiceCollection AddApiPresentation(this IServiceCollection services, IHostEnvironment environment, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddSingleton<IDocumentUploadValidator, DocumentUploadValidator>();
        services.AddSingleton<IDocumentUploadCommandFactory, DocumentUploadCommandFactory>();
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<ChatRequestValidator>();
        services.Configure<ApiBehaviorOptions>(options =>
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

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Description = environment.IsDevelopment()
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

        services.AddCors(options =>
        {
            options.AddPolicy("ConfiguredOrigins", policy =>
            {
                var allowedOrigins = ResolveAllowedOrigins(environment, configuration);

                if (environment.IsDevelopment())
                {
                    policy.SetIsOriginAllowed(origin => IsConfiguredOrigin(origin, allowedOrigins) || IsLocalDevelopmentOrigin(origin))
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                        .AllowAnyHeader();
                    return;
                }

                policy.WithOrigins(allowedOrigins)
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                    .AllowAnyHeader();
            });
        });

        return services;
    }

    private static string[] ResolveAllowedOrigins(IHostEnvironment environment, IConfiguration configuration)
    {
        var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?.Where(IsAbsoluteOrigin)
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

    private static bool IsAbsoluteOrigin(string? origin)
    {
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(uri.Host);
    }

    private static bool IsConfiguredOrigin(string? origin, IEnumerable<string> allowedOrigins)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var normalizedOrigin = uri.GetLeftPart(UriPartial.Authority);
        return allowedOrigins.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsLocalDevelopmentOrigin(string? origin)
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
}