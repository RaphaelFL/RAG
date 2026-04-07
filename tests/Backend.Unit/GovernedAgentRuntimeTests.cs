using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

using Backend.Unit.GovernedAgentRuntimeTestsSupport;

namespace Backend.Unit;

public class GovernedAgentRuntimeTests
{
    [Fact]
    public async Task RunAsync_ShouldReject_WhenToolBudgetIsInvalid()
    {
        var auditWriter = new CapturingOperationalAuditWriter();
        var sut = CreateSut(auditWriter);

        var result = await sut.RunAsync(new AgentRunRequest
        {
            TenantId = Guid.NewGuid(),
            AgentName = "FileSearchAgent",
            ToolBudget = 0,
            Input = new Dictionary<string, object?>
            {
                ["query"] = "politica"
            }
        }, CancellationToken.None);

        result.Status.Should().Be("rejected");
        result.Output["reason"].Should().Be("Tool budget invalido.");
        auditWriter.AgentRuns.Should().ContainSingle();
        auditWriter.AgentRuns[0].Status.Should().Be("rejected");
        auditWriter.ToolExecutions.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_ShouldDispatch_FileSearchAgent_AndWriteAudits()
    {
        var auditWriter = new CapturingOperationalAuditWriter();
        var fileSearchTool = new CapturingFileSearchTool();
        var sut = CreateSut(auditWriter, fileSearchTool: fileSearchTool);

        var result = await sut.RunAsync(new AgentRunRequest
        {
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AgentName = "FileSearchAgent",
            ToolBudget = 3,
            Input = new Dictionary<string, object?>
            {
                ["query"] = "politica de viagens"
            }
        }, CancellationToken.None);

        result.Status.Should().Be("completed");
        fileSearchTool.LastRequest.Should().NotBeNull();
        fileSearchTool.LastRequest!.Query.Should().Be("politica de viagens");
        auditWriter.ToolExecutions.Should().ContainSingle();
        auditWriter.ToolExecutions[0].ToolName.Should().Be("file_search");
        auditWriter.AgentRuns.Should().ContainSingle();
        auditWriter.AgentRuns[0].RemainingBudget.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_ShouldMarkCodeInterpreterExecutionAsFailed_WhenExitCodeIsNonZero()
    {
        var auditWriter = new CapturingOperationalAuditWriter();
        var codeInterpreter = new CapturingCodeInterpreter
        {
            Result = new CodeInterpreterResult
            {
                ExitCode = 2,
                StdErr = "syntax error"
            }
        };
        var sut = CreateSut(auditWriter, codeInterpreter: codeInterpreter);

        var result = await sut.RunAsync(new AgentRunRequest
        {
            TenantId = Guid.NewGuid(),
            AgentName = "CodeInterpreterAgent",
            ToolBudget = 2,
            Input = new Dictionary<string, object?>
            {
                ["code"] = "print("
            }
        }, CancellationToken.None);

        result.Status.Should().Be("completed");
        result.Output["exitCode"].Should().Be(2);
        result.Output["stderr"].Should().Be("syntax error");
        auditWriter.ToolExecutions.Should().ContainSingle();
        auditWriter.ToolExecutions[0].Status.Should().Be("failed");
    }

    [Fact]
    public async Task RunAsync_ShouldReturnFallbackMessage_ForUnknownAgent()
    {
        var auditWriter = new CapturingOperationalAuditWriter();
        var sut = CreateSut(auditWriter);

        var result = await sut.RunAsync(new AgentRunRequest
        {
            TenantId = Guid.NewGuid(),
            AgentName = "UnknownAgent",
            ToolBudget = 2,
            Input = new Dictionary<string, object?>()
        }, CancellationToken.None);

        result.Status.Should().Be("completed");
        result.Output["message"].Should().Be("Agent ainda nao implementado nesta etapa.");
        auditWriter.ToolExecutions.Should().BeEmpty();
        auditWriter.AgentRuns.Should().ContainSingle();
    }

    private static GovernedAgentRuntime CreateSut(
        CapturingOperationalAuditWriter auditWriter,
        CapturingFileSearchTool? fileSearchTool = null,
        CapturingWebSearchTool? webSearchTool = null,
        CapturingCodeInterpreter? codeInterpreter = null,
        CapturingPromptAssembler? promptAssembler = null)
    {
        var handlers = new IGovernedAgentHandler[]
        {
            new FileSearchAgentHandler(fileSearchTool ?? new CapturingFileSearchTool()),
            new WebSearchAgentHandler(webSearchTool ?? new CapturingWebSearchTool()),
            new CodeInterpreterAgentHandler(codeInterpreter ?? new CapturingCodeInterpreter()),
            new PromptAssemblyAgentHandler(promptAssembler ?? new CapturingPromptAssembler())
        };

        return new GovernedAgentRuntime(
            handlers,
            Options.Create(new AgentRuntimeOptions { MaxToolBudget = 4 }),
            auditWriter);
    }
}