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

        var startInfo = new ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(_options.RuntimeCommand) ? "python" : _options.RuntimeCommand,
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