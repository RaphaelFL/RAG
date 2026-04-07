namespace Chatbot.Application.Abstractions;

public sealed class PromptAssemblyResult
{
    public string Prompt { get; set; } = string.Empty;
    public IReadOnlyCollection<string> IncludedChunkIds { get; set; } = Array.Empty<string>();
    public int EstimatedPromptTokens { get; set; }
    public IReadOnlyCollection<string> HumanReadableCitations { get; set; } = Array.Empty<string>();
}
