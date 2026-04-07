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

internal sealed class FakeChatCompletionProvider : IChatCompletionProvider
{
    public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        return Task.FromResult(new ChatCompletionResult
        {
            Message = request.RetrievedChunks.Count == 0
                ? $"Resposta geral para: {request.Message}"
                : $"Resposta fundamentada com {request.RetrievedChunks.Count} chunks.",
            Model = "test-model",
            PromptTokens = 11,
            CompletionTokens = 7,
            TotalTokens = 18
        });
    }
}
