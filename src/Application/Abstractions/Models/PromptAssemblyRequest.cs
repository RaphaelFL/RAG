namespace Chatbot.Application.Abstractions;

public sealed class PromptAssemblyRequest
{
    public Guid TenantId { get; set; }
    public string SystemInstructions { get; set; } = string.Empty;
    public string UserQuestion { get; set; } = string.Empty;
    public IReadOnlyCollection<RetrievedChunk> Chunks { get; set; } = Array.Empty<RetrievedChunk>();
    public int MaxPromptTokens { get; set; }
    public bool AllowGeneralKnowledge { get; set; }
}
