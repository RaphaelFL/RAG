using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Tools;

public sealed class GuardedPythonCodeInterpreter : ICodeInterpreter
{
    private readonly CodeInterpreterOptions _options;
    private readonly GuardedPythonCodePolicy _policy = new();
    private readonly GuardedPythonScriptBuilder _scriptBuilder = new();
    private readonly GuardedPythonProcessRunner _processRunner = new();
    private readonly GuardedPythonExecutionWorkspace _workspace;
    private readonly ILogger<GuardedPythonCodeInterpreter> _logger;

    public GuardedPythonCodeInterpreter(
        IOptions<CodeInterpreterOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<GuardedPythonCodeInterpreter> logger)
    {
        _options = options.Value;
        _workspace = new GuardedPythonExecutionWorkspace(hostEnvironment, _options.WorkingDirectoryRoot);
        _logger = logger;
    }

    public async Task<CodeInterpreterResult> ExecuteAsync(CodeInterpreterRequest request, CancellationToken ct)
    {
        if (!_policy.TryValidate(request, _options, out var validationError))
        {
            return GuardedPythonCodePolicy.Disabled(validationError);
        }

        var execution = _workspace.CreateExecution();

        try
        {
            var copiedArtifacts = _workspace.CopyInputArtifacts(request.InputArtifacts, execution.InputDirectory);
            var scriptContent = _scriptBuilder.Build(request.Code, copiedArtifacts, execution.ExecutionDirectory);
            await File.WriteAllTextAsync(execution.ScriptPath, scriptContent, System.Text.Encoding.UTF8, ct);

            var result = await _processRunner.ExecuteAsync(_options.Runtime, execution, Math.Max(5, _options.TimeoutSeconds), ct);
            result.OutputArtifacts = _workspace.CollectOutputArtifacts(execution);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha na execucao do code interpreter para tenant {TenantId}.", request.TenantId);
            return new CodeInterpreterResult
            {
                ExitCode = -1,
                StdErr = ex.Message,
                StdOut = string.Empty,
                OutputArtifacts = Array.Empty<string>()
            };
        }
    }
}