using Chatbot.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Chatbot.Infrastructure.Observability;

public sealed class SecurityAuditLogger : ISecurityAuditLogger
{
    private readonly ILogger<SecurityAuditLogger> _logger;

    public SecurityAuditLogger(ILogger<SecurityAuditLogger> logger)
    {
        _logger = logger;
    }

    public void LogAuthenticationFailure(string? userId, string reason)
    {
        _logger.LogWarning("Authentication failure for user {UserId}: {Reason}", userId ?? "anonymous", reason);
    }

    public void LogAccessDenied(string? userId, string resource)
    {
        _logger.LogWarning("Access denied for user {UserId} on resource {Resource}", userId ?? "anonymous", resource);
    }

    public void LogFileRejected(string fileName, string reason)
    {
        _logger.LogWarning("Upload rejected for file {FileName}: {Reason}", fileName, reason);
    }

    public void LogProviderFallback(string provider, string fallbackProvider, string reason)
    {
        _logger.LogInformation("Provider fallback triggered from {Provider} to {FallbackProvider}: {Reason}", provider, fallbackProvider, reason);
    }

    public void LogPromptInjectionDetected(string source, string reason)
    {
        _logger.LogWarning("Prompt injection signal detected in {Source}: {Reason}", source, reason);
    }
}
