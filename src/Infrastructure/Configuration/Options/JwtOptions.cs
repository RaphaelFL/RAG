namespace Chatbot.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SecKey { get; set; } = string.Empty;
    public int TokenExpiresHours { get; set; } = 24;
}
