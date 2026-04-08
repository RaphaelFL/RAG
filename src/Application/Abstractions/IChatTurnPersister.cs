namespace Chatbot.Application.Abstractions;

public interface IChatTurnPersister
{
    Task PersistAsync(
        ChatRequestDto request,
        Guid answerId,
        PromptTemplateDefinition template,
        PreparedChatTurn preparedTurn,
        CancellationToken ct);
}