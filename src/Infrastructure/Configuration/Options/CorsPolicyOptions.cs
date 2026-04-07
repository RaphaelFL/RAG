namespace Chatbot.Infrastructure.Configuration;

public sealed class CorsPolicyOptions
{
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
