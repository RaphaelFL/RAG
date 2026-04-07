namespace Chatbot.Application.Contracts;

public sealed class PromptAssemblyRequestDto
{
    public string Question { get; set; } = string.Empty;
    public string SystemInstructions { get; set; } = string.Empty;
    public int MaxPromptTokens { get; set; } = 4000;
    public bool AllowGeneralKnowledge { get; set; }
    public List<RetrievedChunkDtoV2> Chunks { get; set; } = new();
}
