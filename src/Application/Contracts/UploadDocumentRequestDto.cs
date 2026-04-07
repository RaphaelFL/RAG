namespace Chatbot.Application.Contracts;

public class UploadDocumentRequestDto
{
    public string Name { get; set; } = string.Empty;
    public List<string>? Tags { get; set; }
    public List<string>? Categories { get; set; }
}
