namespace Chatbot.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SecKey { get; set; } = string.Empty;
    public int TokenExpiresHours { get; set; } = 24;
}

public sealed class CorsPolicyOptions
{
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}

public sealed class FeatureFlagOptions
{
    public bool EnableSemanticRanking { get; set; }
    public bool EnableMcp { get; set; }
    public bool EnableGraphRag { get; set; }
    public bool EnableRedisCache { get; set; }
}