using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Domain.Entities;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Unit;

public class FileSystemOperationalAuditStoreTests
{
    [Fact]
    public async Task WriteMethods_ShouldAppendJsonLines_ForEachOperationalRecordType()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "rag-audit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileSystemOperationalAuditStore(
                Options.Create(new LocalPersistenceOptions { BasePath = tempPath }),
                new TestHostEnvironment { ContentRootPath = AppContext.BaseDirectory },
                NullLogger<FileSystemOperationalAuditStore>.Instance);

            await store.WriteRetrievalLogAsync(new RetrievalLogRecord
            {
                RetrievalLogId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                QueryText = "politica",
                Strategy = "hybrid-reranked",
                RequestedTopK = 3,
                ReturnedTopK = 2,
                CreatedAtUtc = DateTime.UtcNow
            }, CancellationToken.None);
            await store.WritePromptAssemblyAsync(new PromptAssemblyRecord
            {
                PromptAssemblyId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                PromptTemplateId = "current-state-grounded",
                MaxPromptTokens = 4000,
                UsedPromptTokens = 320,
                PromptBody = "Prompt",
                CreatedAtUtc = DateTime.UtcNow
            }, CancellationToken.None);
            await store.WriteAgentRunAsync(new AgentRunRecord
            {
                AgentRunId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                AgentName = "FileSearchAgent",
                Status = "completed",
                ToolBudget = 5,
                RemainingBudget = 4,
                CreatedAtUtc = DateTime.UtcNow,
                CompletedAtUtc = DateTime.UtcNow
            }, CancellationToken.None);
            await store.WriteToolExecutionAsync(new ToolExecutionRecord
            {
                ToolExecutionId = Guid.NewGuid(),
                AgentRunId = Guid.NewGuid(),
                ToolName = "file_search",
                Status = "completed",
                CreatedAtUtc = DateTime.UtcNow,
                CompletedAtUtc = DateTime.UtcNow
            }, CancellationToken.None);

            var auditDirectory = Path.Combine(tempPath, "operational-audit");
            Directory.GetFiles(auditDirectory, "*.jsonl").Should().HaveCount(4);

            var retrievalLine = await File.ReadAllLinesAsync(Path.Combine(auditDirectory, "retrieval-log.jsonl"));
            JsonDocument.Parse(retrievalLine.Single()).RootElement.GetProperty("queryText").GetString().Should().Be("politica");

            var promptLine = await File.ReadAllLinesAsync(Path.Combine(auditDirectory, "prompt-assembly-log.jsonl"));
            JsonDocument.Parse(promptLine.Single()).RootElement.GetProperty("promptTemplateId").GetString().Should().Be("current-state-grounded");

            var agentLine = await File.ReadAllLinesAsync(Path.Combine(auditDirectory, "agent-run-log.jsonl"));
            JsonDocument.Parse(agentLine.Single()).RootElement.GetProperty("agentName").GetString().Should().Be("FileSearchAgent");

            var toolLine = await File.ReadAllLinesAsync(Path.Combine(auditDirectory, "tool-execution-log.jsonl"));
            JsonDocument.Parse(toolLine.Single()).RootElement.GetProperty("toolName").GetString().Should().Be("file_search");
        }
        finally
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ReadMethods_ShouldReturnLatestRecords_FilteredByTenant()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "rag-audit-" + Guid.NewGuid().ToString("N"));
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        try
        {
            var store = new FileSystemOperationalAuditStore(
                Options.Create(new LocalPersistenceOptions { BasePath = tempPath }),
                new TestHostEnvironment { ContentRootPath = AppContext.BaseDirectory },
                NullLogger<FileSystemOperationalAuditStore>.Instance);

            var tenantAgentRunId = Guid.NewGuid();
            var otherAgentRunId = Guid.NewGuid();

            await store.WriteRetrievalLogAsync(new RetrievalLogRecord
            {
                RetrievalLogId = Guid.NewGuid(),
                TenantId = otherTenantId,
                QueryText = "outra",
                Strategy = "hybrid",
                RequestedTopK = 2,
                ReturnedTopK = 1,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            }, CancellationToken.None);
            await store.WriteRetrievalLogAsync(new RetrievalLogRecord
            {
                RetrievalLogId = Guid.NewGuid(),
                TenantId = tenantId,
                QueryText = "minha consulta",
                Strategy = "hybrid-reranked",
                RequestedTopK = 3,
                ReturnedTopK = 2,
                CreatedAtUtc = DateTime.UtcNow
            }, CancellationToken.None);

            await store.WritePromptAssemblyAsync(new PromptAssemblyRecord
            {
                PromptAssemblyId = Guid.NewGuid(),
                TenantId = otherTenantId,
                PromptTemplateId = "other",
                MaxPromptTokens = 1000,
                UsedPromptTokens = 100,
                PromptBody = "Outro prompt",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            }, CancellationToken.None);
            await store.WritePromptAssemblyAsync(new PromptAssemblyRecord
            {
                PromptAssemblyId = Guid.NewGuid(),
                TenantId = tenantId,
                PromptTemplateId = "current-state-grounded",
                MaxPromptTokens = 4000,
                UsedPromptTokens = 250,
                PromptBody = "Prompt atual",
                CreatedAtUtc = DateTime.UtcNow
            }, CancellationToken.None);

            await store.WriteAgentRunAsync(new AgentRunRecord
            {
                AgentRunId = otherAgentRunId,
                TenantId = otherTenantId,
                AgentName = "OtherAgent",
                Status = "completed",
                ToolBudget = 2,
                RemainingBudget = 1,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CompletedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            }, CancellationToken.None);
            await store.WriteAgentRunAsync(new AgentRunRecord
            {
                AgentRunId = tenantAgentRunId,
                TenantId = tenantId,
                AgentName = "FileSearchAgent",
                Status = "completed",
                ToolBudget = 5,
                RemainingBudget = 3,
                CreatedAtUtc = DateTime.UtcNow,
                CompletedAtUtc = DateTime.UtcNow
            }, CancellationToken.None);

            await store.WriteToolExecutionAsync(new ToolExecutionRecord
            {
                ToolExecutionId = Guid.NewGuid(),
                AgentRunId = otherAgentRunId,
                ToolName = "web_search",
                Status = "completed",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CompletedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            }, CancellationToken.None);
            await store.WriteToolExecutionAsync(new ToolExecutionRecord
            {
                ToolExecutionId = Guid.NewGuid(),
                AgentRunId = tenantAgentRunId,
                ToolName = "file_search",
                Status = "completed",
                CreatedAtUtc = DateTime.UtcNow,
                CompletedAtUtc = DateTime.UtcNow
            }, CancellationToken.None);

            var retrievals = await store.ReadRetrievalLogsAsync(tenantId, 10, CancellationToken.None);
            var prompts = await store.ReadPromptAssembliesAsync(tenantId, 10, CancellationToken.None);
            var agentRuns = await store.ReadAgentRunsAsync(tenantId, 10, CancellationToken.None);
            var tools = await store.ReadToolExecutionsAsync(tenantId, 10, CancellationToken.None);

            retrievals.Should().ContainSingle();
            retrievals.Single().QueryText.Should().Be("minha consulta");

            prompts.Should().ContainSingle();
            prompts.Single().PromptBody.Should().Be("Prompt atual");

            agentRuns.Should().ContainSingle();
            agentRuns.Single().AgentRunId.Should().Be(tenantAgentRunId);

            tools.Should().ContainSingle();
            tools.Single().AgentRunId.Should().Be(tenantAgentRunId);
            tools.Single().ToolName.Should().Be("file_search");
        }
        finally
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ReadAuditFeedAsync_ShouldFilterAndPaginateWithCursor()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "rag-audit-" + Guid.NewGuid().ToString("N"));
        var tenantId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 04, 02, 12, 00, 00, DateTimeKind.Utc);

        try
        {
            var store = new FileSystemOperationalAuditStore(
                Options.Create(new LocalPersistenceOptions { BasePath = tempPath }),
                new TestHostEnvironment { ContentRootPath = AppContext.BaseDirectory },
                NullLogger<FileSystemOperationalAuditStore>.Instance);

            var agentRunId = Guid.NewGuid();

            await store.WriteRetrievalLogAsync(new RetrievalLogRecord
            {
                RetrievalLogId = Guid.NewGuid(),
                TenantId = tenantId,
                QueryText = "consulta antiga",
                Strategy = "hybrid",
                RequestedTopK = 3,
                ReturnedTopK = 2,
                CreatedAtUtc = baseTime.AddMinutes(-30)
            }, CancellationToken.None);
            await store.WritePromptAssemblyAsync(new PromptAssemblyRecord
            {
                PromptAssemblyId = Guid.NewGuid(),
                TenantId = tenantId,
                PromptTemplateId = "current-state-grounded",
                MaxPromptTokens = 4000,
                UsedPromptTokens = 420,
                PromptBody = "Prompt atual",
                CreatedAtUtc = baseTime.AddMinutes(-20)
            }, CancellationToken.None);
            await store.WriteAgentRunAsync(new AgentRunRecord
            {
                AgentRunId = agentRunId,
                TenantId = tenantId,
                AgentName = "PlannerAgent",
                Status = "failed",
                ToolBudget = 4,
                RemainingBudget = 1,
                CreatedAtUtc = baseTime.AddMinutes(-10),
                CompletedAtUtc = baseTime.AddMinutes(-9)
            }, CancellationToken.None);
            await store.WriteToolExecutionAsync(new ToolExecutionRecord
            {
                ToolExecutionId = Guid.NewGuid(),
                AgentRunId = agentRunId,
                ToolName = "web_search",
                Status = "completed",
                CreatedAtUtc = baseTime.AddMinutes(-5),
                CompletedAtUtc = baseTime.AddMinutes(-4)
            }, CancellationToken.None);

            var filtered = await store.ReadAuditFeedAsync(new OperationalAuditFeedQuery
            {
                TenantId = tenantId,
                Category = "agent-run",
                Status = "failed",
                FromUtc = baseTime.AddMinutes(-15),
                ToUtc = baseTime,
                Limit = 10
            }, CancellationToken.None);

            filtered.Entries.Should().ContainSingle();
            filtered.Entries.Single().Category.Should().Be("agent-run");
            filtered.Entries.Single().Status.Should().Be("failed");
            filtered.NextCursor.Should().BeNull();

            var firstPage = await store.ReadAuditFeedAsync(new OperationalAuditFeedQuery
            {
                TenantId = tenantId,
                Limit = 2
            }, CancellationToken.None);

            firstPage.Entries.Should().HaveCount(2);
            firstPage.Entries.Select(entry => entry.Category).Should().ContainInOrder("tool-execution", "agent-run");
            firstPage.NextCursor.Should().NotBeNullOrWhiteSpace();

            var secondPage = await store.ReadAuditFeedAsync(new OperationalAuditFeedQuery
            {
                TenantId = tenantId,
                Limit = 2,
                Cursor = firstPage.NextCursor
            }, CancellationToken.None);

            secondPage.Entries.Should().HaveCount(2);
            secondPage.Entries.Select(entry => entry.Category).Should().ContainInOrder("prompt-assembly", "retrieval");
            secondPage.NextCursor.Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Backend.Unit";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}