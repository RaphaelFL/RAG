using System.Diagnostics;
using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Tools;

public sealed class GuardedPythonCodeInterpreter : ICodeInterpreter
{
    private static readonly string[] BlockedPatterns =
    [
        "import os",
        "from os",
        "import subprocess",
        "from subprocess",
        "import socket",
        "from socket",
        "import requests",
        "from requests",
        "import urllib",
        "from urllib",
        "import http",
        "from http",
        "import ctypes",
        "from ctypes",
        "import shutil",
        "from shutil",
        "eval(",
        "exec(",
        "compile(",
        "__import__(",
        "os.system",
        "subprocess.",
        "socket.",
        "Path('..",
        "Path(\"..",
        "../",
        "..\\"
    ];

    private readonly CodeInterpreterOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<GuardedPythonCodeInterpreter> _logger;

    public GuardedPythonCodeInterpreter(
        IOptions<CodeInterpreterOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<GuardedPythonCodeInterpreter> logger)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<CodeInterpreterResult> ExecuteAsync(CodeInterpreterRequest request, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return Disabled("Code interpreter desabilitado nesta configuracao.");
        }

        if (!string.Equals(request.Language, "python", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(_options.Runtime, "python", StringComparison.OrdinalIgnoreCase))
        {
            return Disabled("Somente o runtime python esta habilitado nesta etapa.");
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Disabled("Nenhum codigo foi informado para execucao.");
        }

        if (request.Code.Length > 20_000)
        {
            return Disabled("Codigo excede o limite maximo permitido para execucao controlada.");
        }

        if (TryFindBlockedPattern(request.Code, out var blockedPattern))
        {
            return Disabled($"Codigo bloqueado pela policy de seguranca: {blockedPattern}");
        }

        var rootDirectory = ResolveWorkingRoot();
        Directory.CreateDirectory(rootDirectory);

        var executionId = $"run-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var executionDirectory = Path.Combine(rootDirectory, executionId);
        var inputDirectory = Path.Combine(executionDirectory, "inputs");
        Directory.CreateDirectory(inputDirectory);

        try
        {
            var copiedArtifacts = CopyInputArtifacts(request.InputArtifacts, inputDirectory);
            var scriptPath = Path.Combine(executionDirectory, "script.py");
            await File.WriteAllTextAsync(scriptPath, BuildScript(request.Code, copiedArtifacts, executionDirectory), Encoding.UTF8, ct);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.Runtime,
                    Arguments = $"-I -B \"{scriptPath}\"",
                    WorkingDirectory = executionDirectory,
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
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds)));

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

            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;
            var outputArtifacts = CollectOutputArtifacts(executionDirectory, scriptPath, inputDirectory);

            return new CodeInterpreterResult
            {
                ExitCode = process.ExitCode,
                StdOut = TrimOutput(stdOut),
                StdErr = TrimOutput(stdErr),
                OutputArtifacts = outputArtifacts
            };
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

    private string ResolveWorkingRoot()
    {
        return Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, _options.WorkingDirectoryRoot));
    }

    private IReadOnlyCollection<string> CopyInputArtifacts(IReadOnlyCollection<string> artifacts, string inputDirectory)
    {
        var copied = new List<string>();
        var contentRoot = Path.GetFullPath(_hostEnvironment.ContentRootPath);
        foreach (var artifact in artifacts.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var absolutePath = Path.GetFullPath(Path.IsPathRooted(artifact)
                ? artifact
                : Path.Combine(contentRoot, artifact));

            if (!absolutePath.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(absolutePath))
            {
                continue;
            }

            var destination = Path.Combine(inputDirectory, Path.GetFileName(absolutePath));
            File.Copy(absolutePath, destination, overwrite: true);
            copied.Add(destination);
        }

        return copied;
    }

    private static string BuildScript(string code, IReadOnlyCollection<string> copiedArtifacts, string executionDirectory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Auto-generated guarded execution wrapper");
        builder.AppendLine($"INPUT_ARTIFACTS = [{string.Join(", ", copiedArtifacts.Select(path => $"r'''{path.Replace("'", "''", StringComparison.Ordinal)}'''"))}]");
        builder.AppendLine($"OUTPUT_DIR = r'''{executionDirectory.Replace("'", "''", StringComparison.Ordinal)}''' ");
        builder.AppendLine();
        builder.Append(code.Trim());
        builder.AppendLine();
        return builder.ToString();
    }

    private IReadOnlyCollection<string> CollectOutputArtifacts(string executionDirectory, string scriptPath, string inputDirectory)
    {
        var contentRoot = Path.GetFullPath(_hostEnvironment.ContentRootPath);
        return Directory.EnumerateFiles(executionDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(path, scriptPath, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.StartsWith(inputDirectory, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains("__pycache__", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(contentRoot, path))
            .ToArray();
    }

    private static string TrimOutput(string output)
    {
        return output.Length > 12000 ? output[..12000] : output;
    }

    private static bool TryFindBlockedPattern(string code, out string blockedPattern)
    {
        var normalized = code.Replace("\r\n", "\n", StringComparison.Ordinal).ToLowerInvariant();
        blockedPattern = BlockedPatterns.FirstOrDefault(pattern => normalized.Contains(pattern.ToLowerInvariant(), StringComparison.Ordinal)) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(blockedPattern);
    }

    private static CodeInterpreterResult Disabled(string message)
    {
        return new CodeInterpreterResult
        {
            ExitCode = -1,
            StdErr = message,
            StdOut = string.Empty,
            OutputArtifacts = Array.Empty<string>()
        };
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