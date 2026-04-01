using Chatbot.Application.Abstractions;

namespace Chatbot.Application.Agentic;

public sealed class LocalAgenticChatPlanner : IAgenticChatPlanner
{
    public AgenticChatPlan CreatePlan(ChatRequestDto request)
    {
        var templateId = request.TemplateId?.Trim() ?? "grounded_answer";
        var allowGeneralKnowledge = request.Options?.AllowGeneralKnowledge ?? false;

        var requiresRetrieval = templateId switch
        {
            "grounded_answer" => true,
            "comparative_answer" => true,
            "document_summary" => true,
            _ => !allowGeneralKnowledge
        };

        return new AgenticChatPlan
        {
            RequiresRetrieval = requiresRetrieval,
            AllowsGeneralKnowledge = allowGeneralKnowledge,
            PreferStreaming = false,
            ExecutionMode = requiresRetrieval ? "grounded" : "general-knowledge"
        };
    }
}