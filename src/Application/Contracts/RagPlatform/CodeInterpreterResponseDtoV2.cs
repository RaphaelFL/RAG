namespace Chatbot.Application.Contracts;

public sealed class CodeInterpreterResponseDtoV2
{
    public int ExitCode { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
    public List<string> OutputArtifacts { get; set; } = new();
}
