namespace Chatbot.Application.Abstractions;

public sealed class RerankRequest
{
    public Guid TenantId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public IReadOnlyCollection<RetrievedChunk> Candidates { get; set; } = Array.Empty<RetrievedChunk>();
    public int TopK { get; set; }
}
