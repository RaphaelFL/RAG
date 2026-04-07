using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Application.Contracts;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using AppCfg = Chatbot.Application.Configuration;

namespace Backend.Unit;

public class RedisChatSessionStoreTests
{
    [Fact]
    public async Task AppendTurnAsync_AndGetAsync_ShouldPersistThroughFallback_WhenRedisIsDisabled()
    {
        var store = CreateStore(enabled: false);
        var sessionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        await store.AppendTurnAsync(new ChatSessionTurnRecord
        {
            SessionId = sessionId,
            TenantId = tenantId,
            UserId = Guid.NewGuid(),
            AnswerId = Guid.NewGuid(),
            UserMessage = "pergunta",
            AssistantMessage = "resposta",
            TemplateId = "grounded-answer",
            TemplateVersion = "2.0.0",
            TimestampUtc = timestamp,
            Citations = new[]
            {
                new CitationDto
                {
                    DocumentId = Guid.NewGuid(),
                    DocumentTitle = "Manual",
                    ChunkId = "chunk-1",
                    Snippet = "Trecho relevante",
                    Score = 0.91,
                    Location = new LocationDto { Page = 3, Section = "Introducao" }
                }
            },
            Usage = new UsageMetadataDto
            {
                Model = "qwen2.5-coder:7b",
                PromptTokens = 11,
                CompletionTokens = 17,
                TotalTokens = 28,
                LatencyMs = 245,
                RetrievalStrategy = "hybrid"
            }
        }, CancellationToken.None);

        var snapshot = await store.GetAsync(sessionId, tenantId, CancellationToken.None);

        snapshot.Should().NotBeNull();
        snapshot!.Messages.Should().HaveCount(2);
        snapshot.Messages[1].Role.Should().Be("assistant");
        snapshot.Messages[1].TemplateVersion.Should().Be("2.0.0");
        snapshot.Messages[1].Citations.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAsync_ShouldRespectTenantIsolation()
    {
        var store = CreateStore(enabled: false);
        var sessionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await store.AppendTurnAsync(new ChatSessionTurnRecord
        {
            SessionId = sessionId,
            TenantId = tenantId,
            UserId = Guid.NewGuid(),
            AnswerId = Guid.NewGuid(),
            UserMessage = "pergunta",
            AssistantMessage = "resposta",
            TemplateId = "grounded-answer",
            TemplateVersion = "1.0.0",
            TimestampUtc = DateTime.UtcNow,
            Usage = new UsageMetadataDto { Model = "model" }
        }, CancellationToken.None);

        var snapshot = await store.GetAsync(sessionId, Guid.NewGuid(), CancellationToken.None);

        snapshot.Should().BeNull();
    }

    private static RedisChatSessionStore CreateStore(bool enabled)
    {
        return new RedisChatSessionStore(
            new InMemoryChatSessionStore(),
            Options.Create(new AppCfg.RedisCoordinationOptions
            {
                Enabled = enabled,
                KeyPrefix = "chatbot-test"
            }),
            Options.Create(new RedisSettings
            {
                Server = "localhost",
                Port = 6379
            }),
            NullLogger<RedisChatSessionStore>.Instance);
    }
}