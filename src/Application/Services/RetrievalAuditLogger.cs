using System.Text.Json;
using Chatbot.Application.Abstractions;

namespace Chatbot.Application.Services;

public sealed class RetrievalAuditLogger : IRetrievalAuditLogger
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IOperationalAuditWriter _operationalAuditWriter;
    private readonly IRequestContextAccessor _requestContextAccessor;

    public RetrievalAuditLogger(
        IOperationalAuditWriter operationalAuditWriter,
        IRequestContextAccessor requestContextAccessor)
    {
        _operationalAuditWriter = operationalAuditWriter;
        _requestContextAccessor = requestContextAccessor;
    }

    public Task WriteAsync(RetrievalAuditEntry entry, CancellationToken ct)
    {
        return _operationalAuditWriter.WriteRetrievalLogAsync(new RetrievalLogRecord
        {
            RetrievalLogId = Guid.NewGuid(),
            TenantId = _requestContextAccessor.TenantId ?? Guid.Empty,
            QueryText = entry.Query.Query,
            Strategy = entry.Result.RetrievalStrategy,
            RequestedTopK = entry.Plan.RequestedTopK,
            ReturnedTopK = entry.Result.Chunks.Count,
            FiltersJson = JsonSerializer.Serialize(entry.Plan.Filters, SerializerOptions),
            DiagnosticsJson = JsonSerializer.Serialize(entry.Diagnostics, SerializerOptions),
            CreatedAtUtc = DateTime.UtcNow
        }, ct);
    }
}