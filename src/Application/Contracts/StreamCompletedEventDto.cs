namespace Chatbot.Application.Contracts;

public class StreamCompletedEventDto
{
    public UsageMetadataDto Usage { get; set; } = new();
}
