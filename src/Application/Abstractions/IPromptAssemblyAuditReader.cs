namespace Chatbot.Application.Abstractions;

public interface IPromptAssemblyAuditReader
{
    Task<IReadOnlyCollection<PromptAssemblyRecord>> ReadPromptAssembliesAsync(Guid tenantId, int limit, CancellationToken ct);
}