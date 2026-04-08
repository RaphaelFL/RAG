namespace Chatbot.Application.Abstractions;

public interface IChatTurnPreparer
{
    Task<PreparedChatTurn> PrepareAsync(
        ChatRequestDto request,
        PromptTemplateDefinition template,
        AgenticChatPlan plan,
        DateTime startTime,
        CancellationToken ct);
}