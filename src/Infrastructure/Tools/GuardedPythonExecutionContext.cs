namespace Chatbot.Infrastructure.Tools;

internal sealed record GuardedPythonExecutionContext(
    string ContentRoot,
    string ExecutionDirectory,
    string InputDirectory,
    string ScriptPath);