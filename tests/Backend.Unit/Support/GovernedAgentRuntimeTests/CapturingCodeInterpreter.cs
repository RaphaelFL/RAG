using Chatbot.Application.Abstractions;

namespace Backend.Unit.GovernedAgentRuntimeTestsSupport;

internal sealed class CapturingCodeInterpreter : ICodeInterpreter
{
    public CodeInterpreterRequest? LastRequest { get; private set; }

    public CodeInterpreterResult Result { get; set; } = new();

    public Task<CodeInterpreterResult> ExecuteAsync(CodeInterpreterRequest request, CancellationToken ct)
    {
        LastRequest = request;
        return Task.FromResult(Result);
    }
}