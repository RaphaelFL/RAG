using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Embeddings;

public sealed class InternalProcessEmbeddingProvider : IEmbeddingProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, bool> VerifiedRuntimeDependencies = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim DependencyBootstrapLock = new(1, 1);

    private readonly EmbeddingGenerationOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<InternalProcessEmbeddingProvider> _logger;
    private readonly SemaphoreSlim _semaphore;

    public InternalProcessEmbeddingProvider(
        IOptions<EmbeddingGenerationOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<InternalProcessEmbeddingProvider> logger)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
        _semaphore = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));
    }

    public async Task<float[]> CreateEmbeddingAsync(string text, string? modelOverride, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<float>();
        }

        await _semaphore.WaitAsync(ct);
        try
        {
            var startedAt = Stopwatch.GetTimestamp();
            var response = await ExecuteRuntimeAsync(text, modelOverride, ct);
            ChatbotTelemetry.EmbeddingLatencyMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                new KeyValuePair<string, object?>("embedding.runtime", _options.PrimaryRuntime));
            return response.Vectors.FirstOrDefault() ?? Array.Empty<float>();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<EmbeddingRuntimeResponse> ExecuteRuntimeAsync(string text, string? modelOverride, CancellationToken ct)
    {
        var scriptPath = ResolvePath(_options.RuntimeScriptPath);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Script do runtime interno de embeddings nao encontrado: {scriptPath}", scriptPath);
        }

        await EnsureRuntimeDependenciesAsync(scriptPath, ct);

        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveRuntimeCommand(),
            WorkingDirectory = ResolveWorkingDirectory(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in BuildArguments(scriptPath))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
        {
            throw new InvalidOperationException("Nao foi possivel iniciar o runtime interno de embeddings.");
        }

        var payload = new EmbeddingRuntimeRequest
        {
            ModelName = string.IsNullOrWhiteSpace(modelOverride) ? _options.ModelName : modelOverride,
            ModelVersion = _options.ModelVersion,
            ModelPath = ResolveModelPath(),
            NormalizeVectors = _options.NormalizeVectors,
            ExpectedDimensions = _options.Dimensions,
            Texts = new[] { text }
        };

        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds)));

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var standardErrorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
        await process.WaitForExitAsync(timeoutCts.Token);

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Runtime interno de embeddings retornou exit code {process.ExitCode}: {standardError}");
        }

        var response = JsonSerializer.Deserialize<EmbeddingRuntimeResponse>(standardOutput, SerializerOptions);
        if (response is null || response.Vectors.Count == 0)
        {
            throw new InvalidOperationException("Runtime interno de embeddings retornou resposta vazia ou invalida.");
        }

        return response;
    }

    private async Task EnsureRuntimeDependenciesAsync(string scriptPath, CancellationToken ct)
    {
        var requirementsPath = ResolveRequirementsPath(scriptPath);
        if (requirementsPath is null)
        {
            return;
        }

        var runtimeKey = $"{ResolveRuntimeCommand()}|{requirementsPath}";
        if (VerifiedRuntimeDependencies.ContainsKey(runtimeKey))
        {
            return;
        }

        await DependencyBootstrapLock.WaitAsync(ct);
        try
        {
            if (VerifiedRuntimeDependencies.ContainsKey(runtimeKey))
            {
                return;
            }

            if (!await ProbeRuntimeDependenciesAsync(ct))
            {
                _logger.LogWarning("Dependencias do runtime interno de embeddings ausentes. Instalando a partir de {RequirementsPath}.", requirementsPath);
                await InstallRuntimeDependenciesAsync(requirementsPath, ct);

                if (!await ProbeRuntimeDependenciesAsync(ct))
                {
                    throw new InvalidOperationException($"Nao foi possivel preparar o runtime interno de embeddings com as dependencias declaradas em {requirementsPath}.");
                }

                _logger.LogInformation("Dependencias do runtime interno de embeddings instaladas com sucesso.");
            }

            VerifiedRuntimeDependencies.TryAdd(runtimeKey, true);
        }
        finally
        {
            DependencyBootstrapLock.Release();
        }
    }

    private async Task<bool> ProbeRuntimeDependenciesAsync(CancellationToken ct)
    {
        var (exitCode, _, _) = await RunUtilityProcessAsync(
            new[]
            {
                "-c",
                "import sentence_transformers; import torch; import google.protobuf"
            },
            Math.Max(15, _options.TimeoutSeconds),
            ct);

        return exitCode == 0;
    }

    private async Task InstallRuntimeDependenciesAsync(string requirementsPath, CancellationToken ct)
    {
        var (exitCode, _, standardError) = await RunUtilityProcessAsync(
            new[]
            {
                "-m",
                "pip",
                "install",
                "--disable-pip-version-check",
                "-r",
                requirementsPath
            },
            Math.Max(300, _options.TimeoutSeconds * 4),
            ct);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Falha ao instalar dependencias do runtime interno de embeddings: {standardError}");
        }
    }

    private async Task<(int ExitCode, string StandardOutput, string StandardError)> RunUtilityProcessAsync(
        IEnumerable<string> arguments,
        int timeoutSeconds,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveRuntimeCommand(),
            WorkingDirectory = ResolveWorkingDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
        {
            throw new InvalidOperationException("Nao foi possivel iniciar o utilitario do runtime interno de embeddings.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var standardErrorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
        await process.WaitForExitAsync(timeoutCts.Token);

        return (process.ExitCode, await standardOutputTask, await standardErrorTask);
    }

    private string ResolveModelPath()
    {
        if (string.IsNullOrWhiteSpace(_options.ModelPath))
        {
            return string.Empty;
        }

        return ResolvePath(_options.ModelPath);
    }

    private string ResolveWorkingDirectory()
    {
        return string.IsNullOrWhiteSpace(_options.WorkingDirectory)
            ? _hostEnvironment.ContentRootPath
            : ResolvePath(_options.WorkingDirectory);
    }

    private string ResolveRuntimeCommand()
    {
        return string.IsNullOrWhiteSpace(_options.RuntimeCommand) ? "python" : _options.RuntimeCommand;
    }

    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var probedPath = ProbeRelativePath(path);
        if (probedPath is not null)
        {
            return probedPath;
        }

        return Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, path));
    }

    private string? ProbeRelativePath(string path)
    {
        var currentDirectory = new DirectoryInfo(_hostEnvironment.ContentRootPath);

        while (currentDirectory is not null)
        {
            var candidate = Path.GetFullPath(Path.Combine(currentDirectory.FullName, path));
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return null;
    }

    private static string? ResolveRequirementsPath(string scriptPath)
    {
        var scriptDirectory = Path.GetDirectoryName(scriptPath);
        if (string.IsNullOrWhiteSpace(scriptDirectory))
        {
            return null;
        }

        var requirementsPath = Path.Combine(scriptDirectory, "requirements.txt");
        return File.Exists(requirementsPath) ? requirementsPath : null;
    }

    private IEnumerable<string> BuildArguments(string scriptPath)
    {
        yield return scriptPath;

        if (string.IsNullOrWhiteSpace(_options.RuntimeArguments))
        {
            yield break;
        }

        foreach (var argument in SplitArguments(_options.RuntimeArguments))
        {
            yield return argument;
        }
    }

    private static IEnumerable<string> SplitArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            yield break;
        }

        var builder = new StringBuilder();
        var insideQuotes = false;

        foreach (var character in arguments)
        {
            if (character == '"')
            {
                insideQuotes = !insideQuotes;
                continue;
            }

            if (!insideQuotes && char.IsWhiteSpace(character))
            {
                if (builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }

                continue;
            }

            builder.Append(character);
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }
}