namespace Chatbot.Application.Abstractions;

public interface IProviderSecurityAuditLogger
{
    void LogProviderFallback(string provider, string fallbackProvider, string reason);
}