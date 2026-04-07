namespace Chatbot.Application.Contracts;

public sealed class WebSearchResponseDtoV2
{
    public List<WebSearchHitDtoV2> Hits { get; set; } = new();
}
