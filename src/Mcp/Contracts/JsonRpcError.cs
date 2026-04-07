namespace Chatbot.Mcp;

public sealed class JsonRpcError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}
