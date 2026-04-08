using Chatbot.Application.Abstractions;

namespace Chatbot.Application.Services;

internal sealed class ChatTurnPersister : IChatTurnPersister
{
    private readonly IChatSessionStore _chatSessionStore;
    private readonly IRequestContextAccessor _requestContextAccessor;

    public ChatTurnPersister(IChatSessionStore chatSessionStore, IRequestContextAccessor requestContextAccessor)
    {
        _chatSessionStore = chatSessionStore;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task PersistAsync(
        ChatRequestDto request,
        Guid answerId,
        PromptTemplateDefinition template,
        PreparedChatTurn preparedTurn,
        CancellationToken ct)
    {
        await _chatSessionStore.AppendTurnAsync(new ChatSessionTurnRecord
        {
            SessionId = request.SessionId,
            TenantId = _requestContextAccessor.TenantId ?? Guid.Empty,
            UserId = ParseUserId(_requestContextAccessor.UserId),
            AnswerId = answerId,
            UserMessage = request.Message,
            AssistantMessage = preparedTurn.ResponseMessage,
            Citations = preparedTurn.Citations,
            Usage = preparedTurn.Usage,
            TemplateId = template.TemplateId,
            TemplateVersion = template.Version,
            TimestampUtc = DateTime.UtcNow
        }, ct);
    }

    private static Guid ParseUserId(string? rawUserId)
    {
        return Guid.TryParse(rawUserId, out var parsedUserId)
            ? parsedUserId
            : Guid.Empty;
    }
}