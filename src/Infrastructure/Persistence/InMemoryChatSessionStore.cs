using System.Collections.Concurrent;
using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

public sealed class InMemoryChatSessionStore : IChatSessionStore
{
    private static readonly ConcurrentDictionary<Guid, ChatSessionSnapshot> Sessions = new();

    public Task AppendTurnAsync(ChatSessionTurnRecord record, CancellationToken ct)
    {
        Sessions.AddOrUpdate(
            record.SessionId,
            _ => ChatSessionSnapshotFactory.Create(record),
            (_, existing) => ChatSessionSnapshotFactory.Append(existing, record));

        return Task.CompletedTask;
    }

    public Task<ChatSessionSnapshot?> GetAsync(Guid sessionId, Guid tenantId, CancellationToken ct)
    {
        if (!Sessions.TryGetValue(sessionId, out var session) || session.TenantId != tenantId)
        {
            return Task.FromResult<ChatSessionSnapshot?>(null);
        }

        return Task.FromResult<ChatSessionSnapshot?>(ChatSessionSnapshotFactory.Clone(session));
    }
}