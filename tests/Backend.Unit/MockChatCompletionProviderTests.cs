using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Unit;

public class MockChatCompletionProviderTests
{
    [Fact]
    public async Task CompleteAsync_ShouldSummarizeReadableEvidence_InsteadOfChunkIds()
    {
        var provider = CreateProvider();

        var result = await provider.CompleteAsync(new ChatCompletionRequest
        {
            Message = "Quais sao as regras de reembolso?",
            AllowGeneralKnowledge = false,
            RetrievedChunks =
            [
                new RetrievedChunkDto
                {
                    ChunkId = "chunk-1",
                    Content = "As regras de reembolso corporativo exigem comprovante e envio em ate 30 dias.",
                    DocumentTitle = "Manual de reembolso"
                }
            ]
        }, CancellationToken.None);

        result.Message.Should().Contain("reembolso corporativo");
        result.Message.Should().NotContain("chunk-1");
    }

    [Fact]
    public async Task CompleteAsync_ShouldAcknowledgeGeneralKnowledgeMode_WhenNoEvidenceExists()
    {
        var provider = CreateProvider();

        var result = await provider.CompleteAsync(new ChatCompletionRequest
        {
            Message = "Explique a politica de viagens internacionais.",
            AllowGeneralKnowledge = true,
            RetrievedChunks = Array.Empty<RetrievedChunkDto>()
        }, CancellationToken.None);

        result.Message.Should().Contain("Conhecimento geral esta habilitado");
        result.Message.Should().Contain("politica de viagens internacionais");
    }

    private static MockChatCompletionProvider CreateProvider()
    {
        return new MockChatCompletionProvider(Options.Create(new ChatModelOptions
        {
            Model = "gpt-4.1"
        }));
    }
}