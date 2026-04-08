using System.Diagnostics;
using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Tools;

internal sealed class GuardedPythonProcessRunner
{
    public async Task<CodeInterpreterResult> ExecuteAsync(string runtime, GuardedPythonExecutionContext execution, int timeoutSeconds, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = runtime,
                Arguments = $"-I -B \"{execution.ScriptPath}\"",
                WorkingDirectory = execution.ExecutionDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.StartInfo.Environment["PYTHONNOUSERSITE"] = "1";
        process.StartInfo.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        process.StartInfo.Environment["PYTHONUNBUFFERED"] = "1";

        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            TryKill(process);
            return new CodeInterpreterResult
            {
                ExitCode = -1,
                StdErr = "Tempo maximo excedido na execucao do code interpreter.",
                StdOut = string.Empty,
                OutputArtifacts = Array.Empty<string>()
            };
        }

        return new CodeInterpreterResult
        {
            ExitCode = process.ExitCode,
            StdOut = TrimOutput(await stdOutTask),
            StdErr = TrimOutput(await stdErrTask),
            OutputArtifacts = Array.Empty<string>()
        };
    }

    private static string TrimOutput(string output)
    {
        return output.Length > 12000 ? output[..12000] : output;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}