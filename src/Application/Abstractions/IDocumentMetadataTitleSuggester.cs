namespace Chatbot.Application.Abstractions;

public interface IDocumentMetadataTitleSuggester
{
    string Suggest(string fileName, string extractedText);
}