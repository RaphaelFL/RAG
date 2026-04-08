namespace Chatbot.Application.Abstractions;

public interface ISecurityAuditLogger : IAuthenticationSecurityAuditLogger, IFileSecurityAuditLogger, IProviderSecurityAuditLogger, IPromptSecurityAuditLogger
{
}