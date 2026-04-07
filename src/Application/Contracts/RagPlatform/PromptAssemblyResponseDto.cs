namespace Chatbot.Application.Contracts;

public sealed class PromptAssemblyResponseDto
{
    public string Prompt { get; set; } = string.Empty;
    public int EstimatedPromptTokens { get; set; }
    public List<string> IncludedChunkIds { get; set; } = new();
    public List<string> Citations { get; set; } = new();
}
