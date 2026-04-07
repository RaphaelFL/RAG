namespace Chatbot.Application.Abstractions;

public sealed class WebSearchRequest
{
    public Guid TenantId { get; set; }
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; }
}
