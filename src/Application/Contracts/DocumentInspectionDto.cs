namespace Chatbot.Application.Contracts;

public class DocumentInspectionDto
{
    public DocumentDetailsDto Document { get; set; } = new();
    public int EmbeddedChunkCount { get; set; }
    public int TotalChunkCount { get; set; }
    public int FilteredChunkCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<DocumentChunkInspectionDto> Chunks { get; set; } = new();
}
