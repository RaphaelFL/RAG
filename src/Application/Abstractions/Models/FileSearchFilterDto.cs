namespace Chatbot.Application.Abstractions;

public class FileSearchFilterDto
{
    public List<Guid>? DocumentIds { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Categories { get; set; }
    public List<string>? ContentTypes { get; set; }
    public List<string>? Sources { get; set; }
    public Guid? TenantId { get; set; }
}
