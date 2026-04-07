namespace Chatbot.Application.Abstractions;

public class PageExtractionDto
{
    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? WorksheetName { get; set; }
    public int? SlideNumber { get; set; }
    public string? SectionTitle { get; set; }
    public string? TableId { get; set; }
    public string? FormId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public List<TableDto>? Tables { get; set; }
}
