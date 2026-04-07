namespace Chatbot.Application.Abstractions;

public sealed class StructuralSpan
{
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public string? WorksheetName { get; set; }
    public int? SlideNumber { get; set; }
    public string Text { get; set; } = string.Empty;
}
