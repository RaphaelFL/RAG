namespace Chatbot.Domain.Entities;

public sealed class IngestionJobRecord
{
    public Guid IngestionJobId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
