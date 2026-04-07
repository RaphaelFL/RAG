namespace Chatbot.Application.Abstractions;

public sealed class CodeInterpreterResult
{
    public int ExitCode { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
    public IReadOnlyCollection<string> OutputArtifacts { get; set; } = Array.Empty<string>();
}
