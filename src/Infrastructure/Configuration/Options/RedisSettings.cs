namespace Chatbot.Infrastructure.Configuration;

public sealed class RedisSettings
{
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 6379;
    public string Password { get; set; } = string.Empty;
}
