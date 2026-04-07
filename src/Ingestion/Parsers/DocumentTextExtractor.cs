using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;

namespace Chatbot.Ingestion.Parsers;

public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    private readonly IEnumerable<IDocumentParser> _parsers;
    private readonly IOcrProvider _ocrProvider;
    private readonly IDocumentExtractionStrategyDecider _documentExtractionStrategyDecider;
    private readonly IDocumentExtractionResultBuilder _documentExtractionResultBuilder;

    public DocumentTextExtractor(
        IEnumerable<IDocumentParser> parsers,
        IOcrProvider ocrProvider,
        IDocumentExtractionStrategyDecider documentExtractionStrategyDecider,
        IDocumentExtractionResultBuilder documentExtractionResultBuilder)
    {
        _parsers = parsers;
        _ocrProvider = ocrProvider;
        _documentExtractionStrategyDecider = documentExtractionStrategyDecider;
        _documentExtractionResultBuilder = documentExtractionResultBuilder;
    }

    public async Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        DocumentParseResultDto? parseResult = null;
        string? parsedText = null;

        foreach (var parser in _parsers)
        {
            if (!parser.CanParse(command))
            {
                continue;
            }

            parseResult = await parser.ParseAsync(command, ct);
            parsedText = parseResult?.Text;
            if (_documentExtractionStrategyDecider.ShouldUseDirectText(command, parsedText))
            {
                if (_documentExtractionStrategyDecider.ShouldRecordOcrAvoided(command))
                {
                    ChatbotTelemetry.OcrAvoided.Add(1, new KeyValuePair<string, object?>("content.type", command.ContentType));
                }

                var directExtraction = _documentExtractionResultBuilder.Build(parsedText!, "direct", null, parseResult?.Pages, parseResult?.StructuredJson);

                return directExtraction;
            }
        }

        if (!_documentExtractionStrategyDecider.ShouldAttemptOcr(command, parsedText))
        {
            return _documentExtractionResultBuilder.Build(
                parsedText ?? string.Empty,
                string.IsNullOrWhiteSpace(parsedText) ? "unavailable" : "direct",
                null,
                parseResult?.Pages,
                parseResult?.StructuredJson);
        }

        var ocrResult = await _ocrProvider.ExtractAsync(command.Content, command.FileName, ct);
        return _documentExtractionResultBuilder.Build(ocrResult.ExtractedText, "ocr", ocrResult.Provider ?? _ocrProvider.ProviderName, ocrResult.Pages, null);
    }
}