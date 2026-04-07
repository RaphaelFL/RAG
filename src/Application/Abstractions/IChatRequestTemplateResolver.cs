namespace Chatbot.Application.Abstractions;

public interface IChatRequestTemplateResolver
{
    PromptTemplateDefinition Resolve(ChatRequestDto request);
}