namespace Chatbot.Application.Abstractions;

public interface IPromptTemplateRegistry
{
    PromptTemplateDefinition GetRequired(string templateId, string? templateVersion = null);
    IReadOnlyCollection<PromptTemplateDefinition> ListAll();
}