using Chatbot.Application.Abstractions;

namespace Backend.Unit.GovernedAgentRuntimeTestsSupport;

internal sealed class CapturingPromptAssembler : IPromptAssembler
{
    public PromptAssemblyRequest? LastRequest { get; private set; }

    public Task<PromptAssemblyResult> AssembleAsync(PromptAssemblyRequest request, CancellationToken ct)
    {
        LastRequest = request;
        return Task.FromResult(new PromptAssemblyResult
        {
            Prompt = "prompt montado",
            EstimatedPromptTokens = 120,
            IncludedChunkIds = Array.Empty<string>()
        });
    }
}