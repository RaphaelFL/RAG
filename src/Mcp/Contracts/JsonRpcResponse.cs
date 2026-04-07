namespace Chatbot.Mcp;

public sealed class JsonRpcResponse
{
    public string Jsonrpc { get; set; } = "2.0";
    public object? Result { get; set; }
    public JsonRpcError? Error { get; set; }
    public string? Id { get; set; }
}
