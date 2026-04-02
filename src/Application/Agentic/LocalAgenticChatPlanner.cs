using Chatbot.Application.Abstractions;
using System.Text.RegularExpressions;

namespace Chatbot.Application.Agentic;

public sealed class LocalAgenticChatPlanner : IAgenticChatPlanner
{
    private static readonly Regex DocumentCueRegex = new(
        "\\b(documento|arquivo|anexo|manual|politica|pol[ií]tica|contrato|procedimento|processo|base|regulamento|norma|relat[oó]rio|compliance|segundo o documento|na base|nesse arquivo|neste arquivo|resuma|sumarize|summary)\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public AgenticChatPlan CreatePlan(ChatRequestDto request)
    {
        var templateId = request.TemplateId?.Trim() ?? "grounded_answer";
        var message = request.Message?.Trim() ?? string.Empty;
        var hasConversationDocuments = request.Filters?.DocumentIds is { Count: > 0 };
        var mentionsDocuments = DocumentCueRegex.IsMatch(message);

        var requiresRetrieval = templateId switch
        {
            "document_summary" => true,
            "comparative_answer" => hasConversationDocuments || mentionsDocuments,
            "grounded_answer" => hasConversationDocuments || mentionsDocuments,
            _ => hasConversationDocuments || mentionsDocuments
        };

        var allowsGeneralKnowledge = templateId switch
        {
            "document_summary" => false,
            _ => request.Options?.AllowGeneralKnowledge ?? true
        };

        var executionMode = requiresRetrieval
            ? allowsGeneralKnowledge ? "auto-hybrid" : "auto-rag"
            : "auto-llm";

        return new AgenticChatPlan
        {
            RequiresRetrieval = requiresRetrieval,
            AllowsGeneralKnowledge = allowsGeneralKnowledge,
            PreferStreaming = false,
            ExecutionMode = executionMode
        };
    }
}