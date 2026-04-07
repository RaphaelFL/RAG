namespace Chatbot.Application.Contracts;

public class SearchQueryRequestDto
{
    public string Query { get; set; } = string.Empty;
    public SearchFiltersDto? Filters { get; set; }
    public int Top { get; set; } = 5;
    public bool SemanticRanking { get; set; } = true;
}
