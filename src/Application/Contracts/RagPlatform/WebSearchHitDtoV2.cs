namespace Chatbot.Application.Contracts;

public sealed class WebSearchHitDtoV2
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public double Score { get; set; }
}
