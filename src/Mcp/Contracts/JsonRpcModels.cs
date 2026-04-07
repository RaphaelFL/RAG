namespace Chatbot.Mcp;

public sealed class JsonRpcRequest
{
    public string Jsonrpc { get; set; } = "2.0";
    public string Method { get; set; } = string.Empty;
    public object? Params { get; set; }
    public string? Id { get; set; }
}

public sealed class JsonRpcResponse
{
    public string Jsonrpc { get; set; } = "2.0";
    public object? Result { get; set; }
    public JsonRpcError? Error { get; set; }
    public string? Id { get; set; }
}

public sealed class JsonRpcError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}