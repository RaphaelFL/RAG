namespace Chatbot.Application.Abstractions;

public class DocumentParseResultDto
{
    public string Text { get; set; } = string.Empty;
    public string? StructuredJson { get; set; }
    public List<PageExtractionDto> Pages { get; set; } = new();
}
