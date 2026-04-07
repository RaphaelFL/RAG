namespace Chatbot.Application.Contracts;

public class ReindexDocumentRequestDto
{
    public Guid DocumentId { get; set; }
    public bool FullReindex { get; set; }
}
