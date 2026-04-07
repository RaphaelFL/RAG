using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit.RetrievalServiceTestsSupport;

internal sealed class StaticFeatureFlagService : IFeatureFlagService
{
    public bool IsSemanticRankingEnabled { get; set; } = true;
    public bool IsGraphRagEnabled => false;
    public bool IsMcpEnabled => false;
}
