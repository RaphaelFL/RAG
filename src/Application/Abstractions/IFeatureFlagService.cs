namespace Chatbot.Application.Abstractions;

public interface IFeatureFlagService
{
    bool IsSemanticRankingEnabled { get; }
    bool IsGraphRagEnabled { get; }
    bool IsMcpEnabled { get; }
}