using Chatbot.Application.Abstractions;
using Chatbot.Application.Services;
using FluentAssertions;
using Xunit;

namespace Backend.Unit;

public class CurrentStatePromptAssemblerTests
{
    [Fact]
    public async Task AssembleAsync_ShouldDeduplicateChunks_AndIncludeHumanReadableCitations()
    {
        var auditStore = new CapturingOperationalAuditStore();
        var assembler = new CurrentStatePromptAssembler(auditStore);

        var result = await assembler.AssembleAsync(new PromptAssemblyRequest
        {
            TenantId = Guid.NewGuid(),
            SystemInstructions = "Sistema",
            UserQuestion = "Qual a regra?",
            MaxPromptTokens = 4000,
            AllowGeneralKnowledge = false,
            Chunks = new[]
            {
                new RetrievedChunk
                {
                    ChunkId = "c1",
                    DocumentId = Guid.NewGuid(),
                    Score = 0.9,
                    Text = "Texto A",
                    Metadata = new Dictionary<string, string> { ["documentTitle"] = "Doc A" }
                },
                new RetrievedChunk
                {
                    ChunkId = "c1",
                    DocumentId = Guid.NewGuid(),
                    Score = 0.2,
                    Text = "Texto duplicado",
                    Metadata = new Dictionary<string, string> { ["documentTitle"] = "Doc B" }
                }
            }
        }, CancellationToken.None);

        result.IncludedChunkIds.Should().ContainSingle().Which.Should().Be("c1");
        result.Prompt.Should().Contain("Texto A");
        result.Prompt.Should().NotContain("Texto duplicado");
        result.HumanReadableCitations.Should().ContainSingle().Which.Should().Contain("Doc A");
        auditStore.LastPromptAssembly.Should().NotBeNull();
        auditStore.LastPromptAssembly!.PromptBody.Should().Contain("Texto A");
        auditStore.LastPromptAssembly.PromptTemplateId.Should().Be("current-state-grounded");
    }

    private sealed class CapturingOperationalAuditStore : IOperationalAuditStore
    {
        public Chatbot.Domain.Entities.PromptAssemblyRecord? LastPromptAssembly { get; private set; }

        public Task WriteRetrievalLogAsync(Chatbot.Domain.Entities.RetrievalLogRecord record, CancellationToken ct) => Task.CompletedTask;

        public Task WritePromptAssemblyAsync(Chatbot.Domain.Entities.PromptAssemblyRecord record, CancellationToken ct)
        {
            LastPromptAssembly = record;
            return Task.CompletedTask;
        }

        public Task WriteAgentRunAsync(Chatbot.Domain.Entities.AgentRunRecord record, CancellationToken ct) => Task.CompletedTask;

        public Task WriteToolExecutionAsync(Chatbot.Domain.Entities.ToolExecutionRecord record, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyCollection<Chatbot.Domain.Entities.RetrievalLogRecord>> ReadRetrievalLogsAsync(Guid tenantId, int limit, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<Chatbot.Domain.Entities.RetrievalLogRecord>>(Array.Empty<Chatbot.Domain.Entities.RetrievalLogRecord>());

        public Task<IReadOnlyCollection<Chatbot.Domain.Entities.PromptAssemblyRecord>> ReadPromptAssembliesAsync(Guid tenantId, int limit, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<Chatbot.Domain.Entities.PromptAssemblyRecord>>(Array.Empty<Chatbot.Domain.Entities.PromptAssemblyRecord>());

        public Task<IReadOnlyCollection<Chatbot.Domain.Entities.AgentRunRecord>> ReadAgentRunsAsync(Guid tenantId, int limit, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<Chatbot.Domain.Entities.AgentRunRecord>>(Array.Empty<Chatbot.Domain.Entities.AgentRunRecord>());

        public Task<IReadOnlyCollection<Chatbot.Domain.Entities.ToolExecutionRecord>> ReadToolExecutionsAsync(Guid tenantId, int limit, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<Chatbot.Domain.Entities.ToolExecutionRecord>>(Array.Empty<Chatbot.Domain.Entities.ToolExecutionRecord>());

        public Task<OperationalAuditFeedResult> ReadAuditFeedAsync(OperationalAuditFeedQuery query, CancellationToken ct)
            => Task.FromResult(new OperationalAuditFeedResult());
    }
}