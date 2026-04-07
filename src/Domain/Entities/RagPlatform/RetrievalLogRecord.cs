namespace Chatbot.Domain.Entities;

public sealed class RetrievalLogRecord
{
    public Guid RetrievalLogId { get; set; }
    public Guid TenantId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public int RequestedTopK { get; set; }
    public int ReturnedTopK { get; set; }
    public string? FiltersJson { get; set; }
    public string? DiagnosticsJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
