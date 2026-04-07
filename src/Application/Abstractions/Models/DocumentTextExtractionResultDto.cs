namespace Chatbot.Application.Abstractions;

public class DocumentTextExtractionResultDto
{
    public string Text { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? StructuredJson { get; set; }
    public List<PageExtractionDto> Pages { get; set; } = new();
}
