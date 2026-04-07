namespace Chatbot.Application.Abstractions;

public sealed class ContentSourceDescriptor
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public Stream Content { get; set; } = Stream.Null;
    public Dictionary<string, string> Metadata { get; set; } = new();
}
