namespace Chatbot.Application.Contracts;

public sealed class CreateIngestionJobRequestDto
{
    public Guid TenantId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string? AccessControlList { get; set; }
    public List<string> Tags { get; set; } = new();
}

public sealed class CreateIngestionJobResponseDto
{
    public Guid IngestionJobId { get; set; }
    public Guid DocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
}