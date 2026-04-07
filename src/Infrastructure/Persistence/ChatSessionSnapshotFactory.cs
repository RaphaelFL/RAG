using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;

namespace Chatbot.Infrastructure.Persistence;

internal static class ChatSessionSnapshotFactory
{
    public static ChatSessionSnapshot Create(ChatSessionTurnRecord record)
    {
        return new ChatSessionSnapshot
        {
            SessionId = record.SessionId,
            TenantId = record.TenantId,
            UserId = record.UserId,
            CreatedAtUtc = record.TimestampUtc,
            UpdatedAtUtc = record.TimestampUtc,
            Messages = CreateMessages(record)
        };
    }

    public static ChatSessionSnapshot Append(ChatSessionSnapshot existing, ChatSessionTurnRecord record)
    {
        lock (existing)
        {
            existing.UpdatedAtUtc = record.TimestampUtc;
            existing.Messages.AddRange(CreateMessages(record));
            return existing;
        }
    }

    public static ChatSessionSnapshot Clone(ChatSessionSnapshot snapshot)
    {
        return new ChatSessionSnapshot
        {
            SessionId = snapshot.SessionId,
            TenantId = snapshot.TenantId,
            UserId = snapshot.UserId,
            CreatedAtUtc = snapshot.CreatedAtUtc,
            UpdatedAtUtc = snapshot.UpdatedAtUtc,
            Messages = snapshot.Messages.Select(CloneMessage).ToList()
        };
    }

    private static List<ChatSessionMessageSnapshot> CreateMessages(ChatSessionTurnRecord record)
    {
        return new List<ChatSessionMessageSnapshot>
        {
            new()
            {
                MessageId = Guid.NewGuid(),
                Role = "user",
                Content = record.UserMessage,
                CreatedAtUtc = record.TimestampUtc
            },
            new()
            {
                MessageId = record.AnswerId,
                Role = "assistant",
                Content = record.AssistantMessage,
                Citations = record.Citations.Select(CloneCitation).ToList(),
                Usage = CloneUsage(record.Usage),
                TemplateVersion = record.TemplateVersion,
                CreatedAtUtc = record.TimestampUtc
            }
        };
    }

    private static ChatSessionMessageSnapshot CloneMessage(ChatSessionMessageSnapshot message)
    {
        return new ChatSessionMessageSnapshot
        {
            MessageId = message.MessageId,
            Role = message.Role,
            Content = message.Content,
            Citations = message.Citations.Select(CloneCitation).ToList(),
            Usage = message.Usage is null ? null : CloneUsage(message.Usage),
            TemplateVersion = message.TemplateVersion,
            CreatedAtUtc = message.CreatedAtUtc
        };
    }

    private static CitationDto CloneCitation(CitationDto citation)
    {
        return new CitationDto
        {
            DocumentId = citation.DocumentId,
            DocumentTitle = citation.DocumentTitle,
            ChunkId = citation.ChunkId,
            Snippet = citation.Snippet,
            Score = citation.Score,
            Location = citation.Location is null
                ? null
                : new LocationDto
                {
                    Page = citation.Location.Page,
                    EndPage = citation.Location.EndPage,
                    Section = citation.Location.Section
                }
        };
    }

    private static UsageMetadataDto CloneUsage(UsageMetadataDto usage)
    {
        return new UsageMetadataDto
        {
            Model = usage.Model,
            PromptTokens = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            TotalTokens = usage.TotalTokens,
            LatencyMs = usage.LatencyMs,
            RetrievalStrategy = usage.RetrievalStrategy
        };
    }
}