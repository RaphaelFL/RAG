namespace Chatbot.Application.Abstractions;

public interface IPromptAssemblyAuditLogger
{
    Task WriteAsync(PromptAssemblyRequest request, PromptAssemblyResult result, CancellationToken ct);
}