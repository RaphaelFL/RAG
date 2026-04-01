using Chatbot.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Configuration;

public sealed class RuntimeFeatureFlagService : IFeatureFlagService
{
    private readonly IOptionsMonitor<FeatureFlagOptions> _options;

    public RuntimeFeatureFlagService(IOptionsMonitor<FeatureFlagOptions> options)
    {
        _options = options;
    }

    public bool IsSemanticRankingEnabled => _options.CurrentValue.EnableSemanticRanking;

    public bool IsGraphRagEnabled => _options.CurrentValue.EnableGraphRag;

    public bool IsMcpEnabled => _options.CurrentValue.EnableMcp;
}