namespace Chatbot.Mcp;

public sealed class JsonRpcRequest
{
    public string Jsonrpc { get; set; } = "2.0";
    public string Method { get; set; } = string.Empty;
    public object? Params { get; set; }
    public string? Id { get; set; }
}
