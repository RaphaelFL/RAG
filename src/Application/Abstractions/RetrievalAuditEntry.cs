namespace Chatbot.Application.Abstractions;

public sealed class RetrievalAuditEntry
{
    public required RetrievalQueryDto Query { get; init; }

    public required RetrievalExecutionPlan Plan { get; init; }

    public required RetrievalResultDto Result { get; init; }

    public required Dictionary<string, object?> Diagnostics { get; init; }
}