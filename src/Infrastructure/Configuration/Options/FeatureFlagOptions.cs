namespace Chatbot.Infrastructure.Configuration;

public sealed class FeatureFlagOptions
{
    public bool EnableSemanticRanking { get; set; }
    public bool EnableMcp { get; set; }
    public bool EnableGraphRag { get; set; }
    public bool EnableRedisCache { get; set; }
}
