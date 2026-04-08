using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Unit;

public class OpenClaudeCliChatCompletionProviderTests
{
    [Fact]
    public async Task CompleteAsync_ShouldReturnCliMessageAndForwardGroundedPrompt()
    {
        var runner = new FakeOpenClaudeCliRunner
        {
            Result = new OpenClaudeCliInvocationResult(0, "Resposta sintetizada", string.Empty)
        };
        var provider = CreateProvider(runner);

        var result = await provider.CompleteAsync(new ChatCompletionRequest
        {
            Message = "Quais sao as regras de reembolso?",
            AllowGeneralKnowledge = false,
            Template = new PromptTemplateDefinition
            {
                TemplateId = "grounded_answer",
                Version = "1.0.0"
            },
            RetrievedChunks =
            [
                new RetrievedChunkDto
                {
                    ChunkId = "chunk-1",
                    Content = "As regras de reembolso corporativo exigem comprovante e envio em ate 30 dias.",
                    DocumentTitle = "Manual de reembolso",
                    PageNumber = 3
                }
            ]
        }, CancellationToken.None);

        result.Message.Should().Be("Resposta sintetizada");
        result.Model.Should().Be("qwen2.5-coder:7b");
        runner.LastInvocation.Should().NotBeNull();
        runner.LastInvocation!.SystemPrompt.Should().Contain("Use apenas o contexto documental fornecido");
        runner.LastInvocation.Prompt.Should().Contain("Pergunta do usuario");
        runner.LastInvocation.Prompt.Should().Contain("Manual de reembolso");
    }

    [Fact]
    public async Task CompleteAsync_ShouldThrow_WhenCliFails()
    {
        var runner = new FakeOpenClaudeCliRunner
        {
            Result = new OpenClaudeCliInvocationResult(2, string.Empty, "falha de runtime")
        };
        var provider = CreateProvider(runner);

        var act = async () => await provider.CompleteAsync(new ChatCompletionRequest
        {
            Message = "Teste",
            Template = new PromptTemplateDefinition
            {
                TemplateId = "grounded_answer",
                Version = "1.0.0"
            }
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*falha de runtime*");
    }

    private static OpenClaudeCliChatCompletionProvider CreateProvider(IOpenClaudeCliRunner runner)
    {
        return new OpenClaudeCliChatCompletionProvider(
            runner,
            Options.Create(new ChatModelOptions
            {
                Model = "qwen2.5-coder:7b",
                MaxPromptContextTokens = 2200
            }),
            Options.Create(new ExternalProviderClientOptions
            {
                TimeoutSeconds = 180,
                OpenAiCompatibleBaseUrl = "http://localhost:11434/v1",
                OpenAiCompatibleChatModel = "qwen2.5-coder:7b"
            }));
    }

    private sealed class FakeOpenClaudeCliRunner : IOpenClaudeCliRunner
    {
        public OpenClaudeCliInvocation? LastInvocation { get; private set; }
        public OpenClaudeCliInvocationResult Result { get; set; } = new(0, string.Empty, string.Empty);

        public Task<OpenClaudeCliInvocationResult> ExecuteAsync(OpenClaudeCliInvocation invocation, CancellationToken ct)
        {
            LastInvocation = invocation;
            return Task.FromResult(Result);
        }
    }
}