namespace Chatbot.Application.Abstractions;

public interface IAuthenticationSecurityAuditLogger
{
    void LogAuthenticationFailure(string? userId, string reason);
    void LogAccessDenied(string? userId, string resource);
}