namespace Chatbot.Application.Abstractions;

public sealed class ChatCompletionRequest
{
    public string Message { get; set; } = string.Empty;
    public PromptTemplateDefinition Template { get; set; } = new();
    public bool AllowGeneralKnowledge { get; set; }
    public IReadOnlyCollection<RetrievedChunkDto> RetrievedChunks { get; set; } = Array.Empty<RetrievedChunkDto>();
}
