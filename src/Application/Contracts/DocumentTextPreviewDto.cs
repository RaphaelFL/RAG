namespace Chatbot.Application.Contracts;

public class DocumentTextPreviewDto
{
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int CharacterCount { get; set; }
    public int ChunkCount { get; set; }
}