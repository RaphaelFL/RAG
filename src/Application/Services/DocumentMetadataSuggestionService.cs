using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;

namespace Chatbot.Application.Services;

public sealed class DocumentMetadataSuggestionService : IDocumentMetadataSuggestionService
{
    private readonly IDocumentMetadataExtractionService _documentMetadataExtractionService;
    private readonly IDocumentMetadataTitleSuggester _documentMetadataTitleSuggester;
    private readonly IDocumentMetadataCategorySuggester _documentMetadataCategorySuggester;
    private readonly IDocumentMetadataTagSuggester _documentMetadataTagSuggester;
    private readonly IDocumentMetadataPreviewBuilder _documentMetadataPreviewBuilder;

    public DocumentMetadataSuggestionService(
        IDocumentMetadataExtractionService documentMetadataExtractionService,
        IDocumentMetadataTitleSuggester documentMetadataTitleSuggester,
        IDocumentMetadataCategorySuggester documentMetadataCategorySuggester,
        IDocumentMetadataTagSuggester documentMetadataTagSuggester,
        IDocumentMetadataPreviewBuilder documentMetadataPreviewBuilder)
    {
        _documentMetadataExtractionService = documentMetadataExtractionService;
        _documentMetadataTitleSuggester = documentMetadataTitleSuggester;
        _documentMetadataCategorySuggester = documentMetadataCategorySuggester;
        _documentMetadataTagSuggester = documentMetadataTagSuggester;
        _documentMetadataPreviewBuilder = documentMetadataPreviewBuilder;
    }

    public async Task<DocumentMetadataSuggestionDto> SuggestAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        var extraction = await _documentMetadataExtractionService.ExtractAsync(command, ct);

        var extractedText = DocumentMetadataTextUtility.NormalizeLineEndings(extraction.Text);
        var normalizedText = DocumentMetadataTextUtility.NormalizeWhitespace(extractedText);
        var suggestedTitle = _documentMetadataTitleSuggester.Suggest(command.FileName, extractedText);
        var suggestedCategory = _documentMetadataCategorySuggester.Suggest(command.FileName, normalizedText);
        var suggestedTags = _documentMetadataTagSuggester.Suggest(command.FileName, suggestedTitle, suggestedCategory, normalizedText);

        return new DocumentMetadataSuggestionDto
        {
            SuggestedTitle = suggestedTitle,
            SuggestedCategory = suggestedCategory,
            SuggestedCategories = string.IsNullOrWhiteSpace(suggestedCategory)
                ? new List<string>()
                : new List<string> { suggestedCategory },
            SuggestedTags = suggestedTags,
            Strategy = $"heuristic-{(string.IsNullOrWhiteSpace(extraction.Strategy) ? "direct" : extraction.Strategy)}",
            PreviewText = _documentMetadataPreviewBuilder.Build(normalizedText)
        };
    }
}