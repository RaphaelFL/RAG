namespace Chatbot.Application.Contracts;

public class StreamStartedEventDto
{
    public Guid AnswerId { get; set; }
    public Guid SessionId { get; set; }
}
