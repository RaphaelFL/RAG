namespace Chatbot.Application.Abstractions;

public interface IDocumentMetadataTagSuggester
{
    List<string> Suggest(string fileName, string suggestedTitle, string? suggestedCategory, string extractedText);
}