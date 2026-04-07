using System.Security.Claims;
using Chatbot.Application.Configuration;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Mcp;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

using Backend.Unit.McpServerTestsSupport;

namespace Backend.Unit;

public class McpServerTests
{
    [Fact]
    public async Task HandleAsync_ShouldReject_WhenMcpIsDisabled()
    {
        var sut = CreateSut(enableMcp: false);

        var result = await sut.HandleAsync(new JsonRpcRequest
        {
            Method = "tools/list",
            Id = "1"
        }, new ClaimsPrincipal(new ClaimsIdentity()), CancellationToken.None);

        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(-32601);
    }

    [Fact]
    public async Task HandleAsync_ShouldDispatch_ToolCalls_ToRegisteredHandler()
    {
        var handler = new CapturingMcpToolHandler("search", McpResponseFactory.Ok("1", new { ok = true }));
        var sut = CreateSut(toolHandlers: new[] { handler });

        var result = await sut.HandleAsync(new JsonRpcRequest
        {
            Method = "tools/call",
            Id = "1",
            Params = new
            {
                name = "search",
                arguments = new
                {
                    query = "politica"
                }
            }
        }, new ClaimsPrincipal(new ClaimsIdentity()), CancellationToken.None);

        result.Error.Should().BeNull();
        handler.CallCount.Should().Be(1);
        handler.LastToolName.Should().Be("search");
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnError_WhenToolHandlerDoesNotExist()
    {
        var sut = CreateSut();

        var result = await sut.HandleAsync(new JsonRpcRequest
        {
            Method = "tools/call",
            Id = "1",
            Params = new
            {
                name = "unknown_tool"
            }
        }, new ClaimsPrincipal(new ClaimsIdentity()), CancellationToken.None);

        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(-32602);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnMethodNotFound_ForUnknownMethod()
    {
        var sut = CreateSut();

        var result = await sut.HandleAsync(new JsonRpcRequest
        {
            Method = "unknown/method",
            Id = "1"
        }, new ClaimsPrincipal(new ClaimsIdentity()), CancellationToken.None);

        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(-32601);
    }

    [Fact]
    public async Task HandleAsync_ShouldRead_RuntimeCapabilitiesResource()
    {
        var sut = CreateSut();

        var result = await sut.HandleAsync(new JsonRpcRequest
        {
            Method = "resources/read",
            Id = "1",
            Params = new
            {
                uri = "rag://runtime-capabilities"
            }
        }, new ClaimsPrincipal(new ClaimsIdentity()), CancellationToken.None);

        result.Error.Should().BeNull();
        result.Result.Should().NotBeNull();
        result.Result.Should().BeEquivalentTo(new
        {
            mcpEnabled = true,
            webSearchEnabled = true,
            codeInterpreterEnabled = true,
            agentRuntimeEnabled = true,
            semanticRankingEnabled = false,
            graphRagEnabled = false
        });
    }

    private static McpServer CreateSut(bool enableMcp = true, IEnumerable<IMcpToolHandler>? toolHandlers = null)
    {
        var promptRegistry = new PromptTemplateRegistry(Options.Create(new PromptTemplateOptions
        {
            GroundedAnswerVersion = "1.0.0",
            DefaultTimeout = 30,
            InsufficientEvidenceMessage = "sem evidencia",
            BlockedInputPatterns = new[] { "ignore previous instructions" }
        }));

        var featureFlags = Options.Create(new FeatureFlagOptions { EnableMcp = enableMcp });
        var embeddingOptions = Options.Create(new EmbeddingGenerationOptions { ModelName = "test", ModelVersion = "1", Dimensions = 3, BatchSize = 1, PrimaryRuntime = "local" });
        var vectorStoreOptions = Options.Create(new VectorStoreOptions { Provider = "memory", DefaultTopK = 5, DefaultScoreThreshold = 0.5, Schema = "v1" });
        var agentRuntimeOptions = Options.Create(new AgentRuntimeOptions { Enabled = true, MaxToolBudget = 4 });
        var webSearchOptions = Options.Create(new WebSearchOptions { Enabled = true, DefaultTopK = 5, AllowedHosts = Array.Empty<string>() });
        var codeInterpreterOptions = Options.Create(new CodeInterpreterOptions { Enabled = true, Runtime = "python" });
        var methodHandlers = new IMcpMethodHandler[]
        {
            new ToolListMethodHandler(),
            new ResourceListMethodHandler(),
            new ResourceReadMethodHandler(promptRegistry, featureFlags, embeddingOptions, vectorStoreOptions, agentRuntimeOptions, webSearchOptions, codeInterpreterOptions),
            new PromptListMethodHandler(promptRegistry),
            new ToolCallMethodHandler(toolHandlers ?? Array.Empty<IMcpToolHandler>())
        };

        return new McpServer(
            methodHandlers,
            featureFlags);
    }
}