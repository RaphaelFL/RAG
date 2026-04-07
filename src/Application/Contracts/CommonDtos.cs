namespace Chatbot.Application.Contracts;

public class ErrorResponseDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string[]>? Details { get; set; }
    public string TraceId { get; set; } = string.Empty;
}