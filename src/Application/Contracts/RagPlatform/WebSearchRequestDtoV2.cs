namespace Chatbot.Application.Contracts;

public sealed class WebSearchRequestDtoV2
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
}
