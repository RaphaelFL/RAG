namespace Chatbot.Application.Abstractions;

public interface IPromptSecurityAuditLogger
{
    void LogPromptInjectionDetected(string source, string reason);
}