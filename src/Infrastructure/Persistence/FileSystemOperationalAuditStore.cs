using Chatbot.Application.Abstractions;
using Chatbot.Domain.Entities;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence;

public sealed class FileSystemOperationalAuditStore : IOperationalAuditWriter, IOperationalAuditReader
{
    private readonly OperationalAuditFileSet _files;
    private readonly JsonLinesOperationalAuditStorage _storage;
    private readonly OperationalAuditFeedBuilder _feedBuilder;

    public FileSystemOperationalAuditStore(
        IOptions<LocalPersistenceOptions> options,
        IHostEnvironment environment,
        ILogger<FileSystemOperationalAuditStore> logger)
    {
        _files = OperationalAuditFileSet.Create(options.Value.BasePath, environment.ContentRootPath);
        _storage = new JsonLinesOperationalAuditStorage(logger);
        _feedBuilder = new OperationalAuditFeedBuilder();
    }

    public Task WriteRetrievalLogAsync(RetrievalLogRecord record, CancellationToken ct)
    {
        return _storage.AppendAsync(_files.RetrievalLogPath, record, ct);
    }

    public Task WritePromptAssemblyAsync(PromptAssemblyRecord record, CancellationToken ct)
    {
        return _storage.AppendAsync(_files.PromptAssemblyLogPath, record, ct);
    }

    public Task WriteAgentRunAsync(AgentRunRecord record, CancellationToken ct)
    {
        return _storage.AppendAsync(_files.AgentRunLogPath, record, ct);
    }

    public Task WriteToolExecutionAsync(ToolExecutionRecord record, CancellationToken ct)
    {
        return _storage.AppendAsync(_files.ToolExecutionLogPath, record, ct);
    }

    public Task<IReadOnlyCollection<RetrievalLogRecord>> ReadRetrievalLogsAsync(Guid tenantId, int limit, CancellationToken ct)
    {
        return _storage.ReadRecordsAsync<RetrievalLogRecord>(_files.RetrievalLogPath, record => record.TenantId == tenantId, limit, ct);
    }

    public Task<IReadOnlyCollection<PromptAssemblyRecord>> ReadPromptAssembliesAsync(Guid tenantId, int limit, CancellationToken ct)
    {
        return _storage.ReadRecordsAsync<PromptAssemblyRecord>(_files.PromptAssemblyLogPath, record => record.TenantId == tenantId, limit, ct);
    }

    public Task<IReadOnlyCollection<AgentRunRecord>> ReadAgentRunsAsync(Guid tenantId, int limit, CancellationToken ct)
    {
        return _storage.ReadRecordsAsync<AgentRunRecord>(_files.AgentRunLogPath, record => record.TenantId == tenantId, limit, ct);
    }

    public async Task<IReadOnlyCollection<ToolExecutionRecord>> ReadToolExecutionsAsync(Guid tenantId, int limit, CancellationToken ct)
    {
        var scopedAgentRunIds = (await ReadAgentRunsAsync(tenantId, Math.Max(limit * 4, limit), ct))
            .Select(record => record.AgentRunId)
            .ToHashSet();

        return await _storage.ReadRecordsAsync<ToolExecutionRecord>(_files.ToolExecutionLogPath, record => scopedAgentRunIds.Contains(record.AgentRunId), limit, ct);
    }

    public async Task<OperationalAuditFeedResult> ReadAuditFeedAsync(OperationalAuditFeedQuery query, CancellationToken ct)
    {
        var retrievals = await ReadRetrievalLogsAsync(query.TenantId, int.MaxValue, ct);
        var prompts = await ReadPromptAssembliesAsync(query.TenantId, int.MaxValue, ct);
        var agentRuns = await ReadAgentRunsAsync(query.TenantId, int.MaxValue, ct);
        var toolExecutions = await ReadToolExecutionsAsync(query.TenantId, int.MaxValue, ct);

        return _feedBuilder.Build(query, retrievals, prompts, agentRuns, toolExecutions);
    }
}