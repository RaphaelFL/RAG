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

internal sealed class StaticAgenticChatPlanner : IAgenticChatPlanner
{
    private readonly AgenticChatPlan _plan;

    public StaticAgenticChatPlanner(AgenticChatPlan? plan = null)
    {
        _plan = plan ?? new AgenticChatPlan
        {
            RequiresRetrieval = true,
            AllowsGeneralKnowledge = false,
            ExecutionMode = "grounded"
        };
    }

    public AgenticChatPlan CreatePlan(ChatRequestDto request) => _plan;
}
