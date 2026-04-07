namespace Chatbot.Domain.Entities;

public sealed class PromptAssemblyRecord
{
    public Guid PromptAssemblyId { get; set; }
    public Guid TenantId { get; set; }
    public string PromptTemplateId { get; set; } = string.Empty;
    public int MaxPromptTokens { get; set; }
    public int UsedPromptTokens { get; set; }
    public string? IncludedChunkIdsJson { get; set; }
    public string PromptBody { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
