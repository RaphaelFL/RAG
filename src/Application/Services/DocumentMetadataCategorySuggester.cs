namespace Chatbot.Application.Services;

public sealed class DocumentMetadataCategorySuggester : IDocumentMetadataCategorySuggester
{
    public string Suggest(string fileName, string extractedText)
    {
        var corpus = DocumentMetadataTextUtility.Tokenize($"{Path.GetFileNameWithoutExtension(fileName)} {extractedText}");
        var bestMatch = DocumentMetadataKeywordCatalog.CategoryRules
            .Select(rule => new
            {
                rule.Category,
                Score = corpus.Count(token => rule.Keywords.Contains(token, StringComparer.OrdinalIgnoreCase))
            })
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Category, StringComparer.Ordinal)
            .FirstOrDefault();

        return bestMatch is null || bestMatch.Score == 0 ? "geral" : bestMatch.Category;
    }
}