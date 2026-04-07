namespace Chatbot.Application.Contracts;

public sealed class CodeInterpreterRequestDtoV2
{
    public string Language { get; set; } = "python";
    public string Code { get; set; } = string.Empty;
    public List<string> InputArtifacts { get; set; } = new();
}
