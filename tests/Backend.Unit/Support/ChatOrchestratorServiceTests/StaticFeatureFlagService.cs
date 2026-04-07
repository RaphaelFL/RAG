using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using Chatbot.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Xunit;

namespace Backend.Unit.ChatOrchestratorServiceTestsSupport;

internal sealed class StaticFeatureFlagService : IFeatureFlagService
{
    public bool IsSemanticRankingEnabled => true;
    public bool IsGraphRagEnabled => false;
    public bool IsMcpEnabled => false;
}
