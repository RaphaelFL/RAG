namespace Chatbot.Application.Abstractions;

public interface IDocumentMetadataPreviewBuilder
{
    string Build(string extractedText);
}