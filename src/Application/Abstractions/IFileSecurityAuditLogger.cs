namespace Chatbot.Application.Abstractions;

public interface IFileSecurityAuditLogger
{
    void LogFileRejected(string fileName, string reason);
}