using Chatbot.Application.Agentic;
using Chatbot.Application.Contracts;
using FluentAssertions;
using Xunit;

namespace Backend.Unit;

public sealed class LocalAgenticChatPlannerTests
{
    [Fact]
    public void CreatePlan_ShouldAlwaysRequireRetrieval_ForGroundedAnswer()
    {
        var sut = new LocalAgenticChatPlanner();

        var plan = sut.CreatePlan(new ChatRequestDto
        {
            Message = "Explique ASO.",
            TemplateId = "grounded_answer",
            Options = new ChatOptionsDto
            {
                AllowGeneralKnowledge = false
            }
        });

        plan.RequiresRetrieval.Should().BeTrue();
        plan.AllowsGeneralKnowledge.Should().BeFalse();
        plan.ExecutionMode.Should().Be("auto-rag");
    }

    [Fact]
    public void CreatePlan_ShouldAlwaysRequireRetrieval_ForComparativeAnswer()
    {
        var sut = new LocalAgenticChatPlanner();

        var plan = sut.CreatePlan(new ChatRequestDto
        {
            Message = "Compare as politicas.",
            TemplateId = "comparative_answer",
            Options = new ChatOptionsDto
            {
                AllowGeneralKnowledge = true
            }
        });

        plan.RequiresRetrieval.Should().BeTrue();
        plan.AllowsGeneralKnowledge.Should().BeTrue();
        plan.ExecutionMode.Should().Be("auto-hybrid");
    }

    [Fact]
    public void CreatePlan_ShouldKeepGeneralQueriesLlmOnly_ForUnknownTemplatesWithoutCues()
    {
        var sut = new LocalAgenticChatPlanner();

        var plan = sut.CreatePlan(new ChatRequestDto
        {
            Message = "Explique o conceito de RAG.",
            TemplateId = "custom_template",
            Options = new ChatOptionsDto
            {
                AllowGeneralKnowledge = true
            }
        });

        plan.RequiresRetrieval.Should().BeFalse();
        plan.AllowsGeneralKnowledge.Should().BeTrue();
        plan.ExecutionMode.Should().Be("auto-llm");
    }
}