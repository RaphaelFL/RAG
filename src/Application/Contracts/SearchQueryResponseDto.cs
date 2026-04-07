namespace Chatbot.Application.Contracts;

public class SearchQueryResponseDto
{
    public List<SearchQueryItemDto> Items { get; set; } = new();
    public int Count { get; set; }
}
