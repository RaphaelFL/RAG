namespace Chatbot.Mcp;

public static class McpResponseFactory
{
    public static JsonRpcResponse Ok(string? id, object? result)
    {
        return new JsonRpcResponse
        {
            Id = id,
            Result = result
        };
    }

    public static JsonRpcResponse Error(string? id, int code, string message)
    {
        return new JsonRpcResponse
        {
            Id = id,
            Error = new JsonRpcError
            {
                Code = code,
                Message = message
            }
        };
    }
}