using Chatbot.Application.Abstractions;

namespace Backend.Unit.IngestionExtractionServiceTestsSupport;

internal sealed class CapturingSecurityAuditLogger : ISecurityAuditLogger
{
    public string? LastSource { get; private set; }
    public string? LastReason { get; private set; }

    public void LogAuthenticationFailure(string? userId, string reason)
    {
    }

    public void LogAccessDenied(string? userId, string resource)
    {
    }

    public void LogFileRejected(string fileName, string reason)
    {
    }

    public void LogProviderFallback(string provider, string fallbackProvider, string reason)
    {
    }

    public void LogPromptInjectionDetected(string source, string reason)
    {
        LastSource = source;
        LastReason = reason;
    }
}