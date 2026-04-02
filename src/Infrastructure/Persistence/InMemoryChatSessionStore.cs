using System.Collections.Concurrent;
using Chatbot.Application.Abstractions;
using Chatbot.Domain.Entities;

namespace Chatbot.Infrastructure.Persistence;

public sealed class InMemoryChatSessionStore : IChatSessionStore
{
    private static readonly ConcurrentDictionary<Guid, ChatSession> Sessions = new();

    public Task AppendTurnAsync(ChatSessionTurnRecord record, CancellationToken ct)
    {
        var session = Sessions.AddOrUpdate(
            record.SessionId,
            _ => CreateSession(record),
            (_, existing) => UpdateSession(existing, record));

        session.UpdatedAtUtc = record.TimestampUtc;
        return Task.CompletedTask;
    }

    public ChatSessionSnapshot? Get(Guid sessionId, Guid tenantId)
    {
        if (!Sessions.TryGetValue(sessionId, out var session) || session.TenantId != tenantId)
        {
            return null;
        }

        return new ChatSessionSnapshot
        {
            SessionId = session.SessionId,
            TenantId = session.TenantId,
            UserId = session.UserId,
            CreatedAtUtc = session.CreatedAtUtc,
            UpdatedAtUtc = session.UpdatedAtUtc,
            Messages = session.Messages.Select(message => new ChatSessionMessageSnapshot
            {
                MessageId = message.MessageId,
                Role = message.Role,
                Content = message.Content,
                Citations = message.Citations.Select(citation => new Chatbot.Application.Contracts.CitationDto
                {
                    DocumentId = citation.DocumentId,
                    DocumentTitle = citation.DocumentTitle,
                    ChunkId = citation.ChunkId,
                    Snippet = citation.Snippet,
                    Score = citation.Score,
                    Location = citation.Location is null
                        ? null
                        : new Chatbot.Application.Contracts.LocationDto
                        {
                            Page = citation.Location.Page,
                            EndPage = citation.Location.EndPage,
                            Section = citation.Location.Section
                        }
                }).ToList(),
                Usage = message.Usage is null
                    ? null
                    : new Chatbot.Application.Contracts.UsageMetadataDto
                    {
                        Model = message.Usage.Model,
                        PromptTokens = message.Usage.PromptTokens,
                        CompletionTokens = message.Usage.CompletionTokens,
                        TotalTokens = message.Usage.TotalTokens,
                        LatencyMs = message.Usage.LatencyMs,
                        RetrievalStrategy = message.Usage.RetrievalStrategy
                    },
                CreatedAtUtc = message.CreatedAtUtc
            }).ToList()
        };
    }

    private static ChatSession CreateSession(ChatSessionTurnRecord record)
    {
        return new ChatSession
        {
            SessionId = record.SessionId,
            TenantId = record.TenantId,
            UserId = record.UserId,
            CreatedAtUtc = record.TimestampUtc,
            UpdatedAtUtc = record.TimestampUtc,
            Messages = CreateMessages(record)
        };
    }

    private static ChatSession UpdateSession(ChatSession existing, ChatSessionTurnRecord record)
    {
        lock (existing)
        {
            existing.UpdatedAtUtc = record.TimestampUtc;
            existing.Messages.AddRange(CreateMessages(record));
            return existing;
        }
    }

    private static List<ChatMessage> CreateMessages(ChatSessionTurnRecord record)
    {
        return new List<ChatMessage>
        {
            new()
            {
                MessageId = Guid.NewGuid(),
                SessionId = record.SessionId,
                Role = "user",
                Content = record.UserMessage,
                CreatedAtUtc = record.TimestampUtc
            },
            new()
            {
                MessageId = record.AnswerId,
                SessionId = record.SessionId,
                Role = "assistant",
                Content = record.AssistantMessage,
                Citations = record.Citations.Select(citation => new Citation
                {
                    DocumentId = citation.DocumentId,
                    DocumentTitle = citation.DocumentTitle,
                    ChunkId = citation.ChunkId,
                    Snippet = citation.Snippet,
                    Score = citation.Score,
                    Location = citation.Location is null
                        ? null
                        : new Location
                        {
                            Page = citation.Location.Page,
                            EndPage = citation.Location.EndPage,
                            Section = citation.Location.Section
                        }
                }).ToList(),
                Usage = new UsageMetadata
                {
                    Model = record.Usage.Model,
                    PromptTokens = record.Usage.PromptTokens,
                    CompletionTokens = record.Usage.CompletionTokens,
                    TotalTokens = record.Usage.TotalTokens,
                    LatencyMs = record.Usage.LatencyMs,
                    RetrievalStrategy = record.Usage.RetrievalStrategy
                },
                CreatedAtUtc = record.TimestampUtc
            }
        };
    }
}