using Chatbot.Application.Abstractions;
using Chatbot.Application.Services;
using FluentAssertions;
using Xunit;

using Backend.Unit.CurrentStatePromptAssemblerTestsSupport;

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

}
