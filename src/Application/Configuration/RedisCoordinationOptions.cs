namespace Chatbot.Application.Configuration;

public sealed class RedisCoordinationOptions
{
    public bool Enabled { get; set; } = true;
    public string Configuration { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = "chatbot";
    public int LockTimeoutSeconds { get; set; } = 60;
}