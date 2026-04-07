namespace Chatbot.Application.Contracts;

public class BulkReindexRequestDto
{
    public List<Guid> DocumentIds { get; set; } = new();
    public bool IncludeAllTenantDocuments { get; set; }
    public string Mode { get; set; } = "incremental";
    public string? Reason { get; set; }
    public string? ForceEmbeddingModel { get; set; }
}
