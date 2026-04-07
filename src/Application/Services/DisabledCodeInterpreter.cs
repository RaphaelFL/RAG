using System.Text;
using System.Text.Json;
using Chatbot.Application.Configuration;
using Chatbot.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Chatbot.Application.Services;

public sealed class DisabledCodeInterpreter : ICodeInterpreter
{
    public Task<CodeInterpreterResult> ExecuteAsync(CodeInterpreterRequest request, CancellationToken ct)
    {
        return Task.FromResult(new CodeInterpreterResult
        {
            ExitCode = -1,
            StdErr = "Code interpreter desabilitado nesta configuracao.",
            StdOut = string.Empty,
            OutputArtifacts = Array.Empty<string>()
        });
    }
}
