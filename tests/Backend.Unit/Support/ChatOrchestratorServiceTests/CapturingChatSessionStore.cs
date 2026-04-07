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

internal sealed class CapturingChatSessionStore : IChatSessionStore
{
    public ChatSessionTurnRecord? LastRecord { get; private set; }

    public Task AppendTurnAsync(ChatSessionTurnRecord record, CancellationToken ct)
    {
        LastRecord = record;
        return Task.CompletedTask;
    }

    public Task<ChatSessionSnapshot?> GetAsync(Guid sessionId, Guid tenantId, CancellationToken ct)
    {
        return Task.FromResult<ChatSessionSnapshot?>(null);
    }
}
