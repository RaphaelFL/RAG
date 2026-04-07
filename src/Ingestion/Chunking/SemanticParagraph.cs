namespace Chatbot.Ingestion.Chunking;

internal sealed class SemanticParagraph
{
    public SemanticParagraph(
        string text,
        int startPage,
        int endPage,
        string section,
        bool isHeading,
        string? worksheetName,
        int? slideNumber,
        string? tableId,
        string? formId,
        int estimatedTokens)
    {
        Text = text;
        StartPage = startPage;
        EndPage = endPage;
        Section = section;
        IsHeading = isHeading;
        WorksheetName = worksheetName;
        SlideNumber = slideNumber;
        TableId = tableId;
        FormId = formId;
        EstimatedTokens = estimatedTokens;
    }

    public string Text { get; }
    public int StartPage { get; }
    public int EndPage { get; }
    public string Section { get; }
    public bool IsHeading { get; }
    public string? WorksheetName { get; }
    public int? SlideNumber { get; }
    public string? TableId { get; }
    public string? FormId { get; }
    public int EstimatedTokens { get; }
}