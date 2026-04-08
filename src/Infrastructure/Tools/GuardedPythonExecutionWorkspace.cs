using Microsoft.Extensions.Hosting;

namespace Chatbot.Infrastructure.Tools;

internal sealed class GuardedPythonExecutionWorkspace
{
    private readonly string _contentRoot;
    private readonly string _workingRoot;

    public GuardedPythonExecutionWorkspace(IHostEnvironment hostEnvironment, string workingDirectoryRoot)
    {
        _contentRoot = Path.GetFullPath(hostEnvironment.ContentRootPath);
        _workingRoot = Path.GetFullPath(Path.Combine(_contentRoot, workingDirectoryRoot));
    }

    public GuardedPythonExecutionContext CreateExecution()
    {
        Directory.CreateDirectory(_workingRoot);
        var executionId = $"run-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var executionDirectory = Path.Combine(_workingRoot, executionId);
        var inputDirectory = Path.Combine(executionDirectory, "inputs");
        Directory.CreateDirectory(inputDirectory);
        return new GuardedPythonExecutionContext(_contentRoot, executionDirectory, inputDirectory, Path.Combine(executionDirectory, "script.py"));
    }

    public IReadOnlyCollection<string> CopyInputArtifacts(IReadOnlyCollection<string> artifacts, string inputDirectory)
    {
        var copied = new List<string>();
        foreach (var artifact in artifacts.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var absolutePath = Path.GetFullPath(Path.IsPathRooted(artifact)
                ? artifact
                : Path.Combine(_contentRoot, artifact));

            if (!absolutePath.StartsWith(_contentRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(absolutePath))
            {
                continue;
            }

            var destination = Path.Combine(inputDirectory, Path.GetFileName(absolutePath));
            File.Copy(absolutePath, destination, overwrite: true);
            copied.Add(destination);
        }

        return copied;
    }

    public IReadOnlyCollection<string> CollectOutputArtifacts(GuardedPythonExecutionContext execution)
    {
        return Directory.EnumerateFiles(execution.ExecutionDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(path, execution.ScriptPath, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.StartsWith(execution.InputDirectory, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains("__pycache__", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(execution.ContentRoot, path))
            .ToArray();
    }
}