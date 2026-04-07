namespace Chatbot.Application.Abstractions;

public sealed class VectorUpsertRequest
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public IReadOnlyCollection<VectorChunkRecord> Chunks { get; set; } = Array.Empty<VectorChunkRecord>();
}
