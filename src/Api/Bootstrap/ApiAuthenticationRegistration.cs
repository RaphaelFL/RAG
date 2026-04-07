using System.Security.Claims;
using System.Text;
using Chatbot.Api.Authentication;
using Chatbot.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.IdentityModel.Tokens;

namespace Chatbot.Api.Bootstrap;

public static class ApiAuthenticationRegistration
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, WebApplicationBuilder builder)
    {
        services
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

        services.AddSingleton<IRequestContextAccessor, RequestContextAccessor>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationMiddlewareResultHandler>();

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy("DocumentAdmin", policy => policy.RequireRole("Analyst", "TenantAdmin", "PlatformAdmin"));
            options.AddPolicy("McpAccess", policy => policy.RequireRole("McpClient", "Analyst", "TenantAdmin", "PlatformAdmin"));
        });

        return services;
    }

    private static bool IsDevelopmentHeaderRequest(HttpRequest request)
    {
        return request.Headers.ContainsKey("X-Tenant-Id")
            || request.Headers.ContainsKey("X-User-Id")
            || request.Headers.ContainsKey("X-User-Role");
    }

    private static SecurityKey? ResolveSigningKey(JwtOptions jwtOptions)
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
}