namespace Chatbot.Application.Contracts;

public class DocumentEmbeddingInspectionDto
{
    public bool Exists { get; set; }
    public int Dimensions { get; set; }
    public List<float> Preview { get; set; } = new();
}
