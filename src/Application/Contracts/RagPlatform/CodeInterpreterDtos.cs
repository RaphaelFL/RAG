namespace Chatbot.Application.Contracts;

public sealed class CodeInterpreterRequestDtoV2
{
    public string Language { get; set; } = "python";
    public string Code { get; set; } = string.Empty;
    public List<string> InputArtifacts { get; set; } = new();
}

public sealed class CodeInterpreterResponseDtoV2
{
    public int ExitCode { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
    public List<string> OutputArtifacts { get; set; } = new();
}