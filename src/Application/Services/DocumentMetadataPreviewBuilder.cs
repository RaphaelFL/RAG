namespace Chatbot.Application.Services;

public sealed class DocumentMetadataPreviewBuilder : IDocumentMetadataPreviewBuilder
{
    public string Build(string extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return string.Empty;
        }

        return extractedText.Length <= 280
            ? extractedText
            : extractedText[..280].TrimEnd() + "...";
    }
}