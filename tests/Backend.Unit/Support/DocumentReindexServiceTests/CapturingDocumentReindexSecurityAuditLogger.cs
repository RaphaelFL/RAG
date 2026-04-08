using Chatbot.Application.Abstractions;

namespace Backend.Unit.DocumentReindexServiceTestsSupport;

internal sealed class CapturingDocumentReindexSecurityAuditLogger : ISecurityAuditLogger
{
    public string? LastAccessDeniedUserId { get; private set; }

    public string? LastAccessDeniedResource { get; private set; }

    public void LogAccessDenied(string? userId, string resource)
    {
        LastAccessDeniedUserId = userId;
        LastAccessDeniedResource = resource;
    }

    public void LogAuthenticationFailure(string? userId, string reason)
    {
    }

    public void LogFileRejected(string fileName, string reason)
    {
    }

    public void LogPromptInjectionDetected(string source, string reason)
    {
    }

    public void LogProviderFallback(string provider, string fallbackProvider, string reason)
    {
    }
}