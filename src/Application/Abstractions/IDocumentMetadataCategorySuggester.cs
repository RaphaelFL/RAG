namespace Chatbot.Application.Abstractions;

public interface IDocumentMetadataCategorySuggester
{
    string Suggest(string fileName, string extractedText);
}