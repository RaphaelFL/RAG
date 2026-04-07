namespace Chatbot.Application.Abstractions;

public interface IPromptAssembler
{
    Task<PromptAssemblyResult> AssembleAsync(PromptAssemblyRequest request, CancellationToken ct);
}