namespace Chatbot.Application.Services;

public sealed class DocumentMetadataTagSuggester : IDocumentMetadataTagSuggester
{
    public List<string> Suggest(string fileName, string suggestedTitle, string? suggestedCategory, string extractedText)
    {
        var corpus = DocumentMetadataTextUtility.Tokenize($"{Path.GetFileNameWithoutExtension(fileName)} {suggestedTitle} {extractedText}");
        var tags = new List<string>();

        if (!string.IsNullOrWhiteSpace(suggestedCategory) && !string.Equals(suggestedCategory, "geral", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add(suggestedCategory);
        }

        foreach (var (_, keywords) in DocumentMetadataKeywordCatalog.CategoryRules)
        {
            foreach (var keyword in keywords)
            {
                if (corpus.Contains(keyword, StringComparer.OrdinalIgnoreCase))
                {
                    tags.Add(keyword);
                }
            }
        }

        foreach (var token in DocumentMetadataTextUtility.Tokenize($"{suggestedTitle} {Path.GetFileNameWithoutExtension(fileName)}"))
        {
            if (token.Length <= 3 || DocumentMetadataKeywordCatalog.StopWords.Contains(token))
            {
                continue;
            }

            tags.Add(token);
        }

        return tags
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }
}