namespace Chatbot.Application.Contracts;

public class DocumentMetadataSuggestionDto
{
    public string SuggestedTitle { get; set; } = string.Empty;
    public string? SuggestedCategory { get; set; }
    public List<string> SuggestedCategories { get; set; } = new();
    public List<string> SuggestedTags { get; set; } = new();
    public string Strategy { get; set; } = string.Empty;
    public string PreviewText { get; set; } = string.Empty;
}
