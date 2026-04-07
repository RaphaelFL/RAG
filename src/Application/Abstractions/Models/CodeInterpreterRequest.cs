namespace Chatbot.Application.Abstractions;

public sealed class CodeInterpreterRequest
{
    public Guid TenantId { get; set; }
    public string Language { get; set; } = "python";
    public string Code { get; set; } = string.Empty;
    public IReadOnlyCollection<string> InputArtifacts { get; set; } = Array.Empty<string>();
}
