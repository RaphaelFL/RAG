namespace Chatbot.Application.Contracts;

public sealed class WebSearchRequestDtoV2
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
}

public sealed class WebSearchResponseDtoV2
{
    public List<WebSearchHitDtoV2> Hits { get; set; } = new();
}

public sealed class WebSearchHitDtoV2
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public double Score { get; set; }
}