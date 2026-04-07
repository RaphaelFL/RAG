namespace Chatbot.Application.Abstractions;

public interface IChatCompletionCacheKeyFactory
{
    string Build(ChatRequestDto request, PromptTemplateDefinition template, AgenticChatPlan plan, IReadOnlyCollection<RetrievedChunkDto> chunks);
}