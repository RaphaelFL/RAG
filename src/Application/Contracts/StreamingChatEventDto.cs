namespace Chatbot.Application.Contracts;

public class StreamingChatEventDto
{
    public string EventType { get; set; } = string.Empty;
    public object? Data { get; set; }
}
