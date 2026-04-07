namespace Chatbot.Application.Abstractions;

public class OcrResultDto
{
    public string ExtractedText { get; set; } = string.Empty;
    public List<PageExtractionDto> Pages { get; set; } = new();
    public string? Provider { get; set; }
}
