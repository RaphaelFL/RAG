namespace Chatbot.Application.Abstractions;

public interface ISecurityAuditLogger
{
    void LogAuthenticationFailure(string? userId, string reason);
    void LogAccessDenied(string? userId, string resource);
    void LogFileRejected(string fileName, string reason);
    void LogProviderFallback(string provider, string fallbackProvider, string reason);
    void LogPromptInjectionDetected(string source, string reason);
}