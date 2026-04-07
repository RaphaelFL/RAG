namespace Chatbot.Application.Contracts;

public class StreamErrorEventDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
}
