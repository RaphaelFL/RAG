namespace Chatbot.Application.Abstractions;

public sealed class FileSearchRequest
{
    public Guid TenantId { get; set; }
    public string Query { get; set; } = string.Empty;
    public Dictionary<string, string[]> Filters { get; set; } = new();
    public int TopK { get; set; }
}
