using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Parsers;

public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    private readonly IEnumerable<IDocumentParser> _parsers;
    private readonly IOcrProvider _ocrProvider;

    public DocumentTextExtractor(IEnumerable<IDocumentParser> parsers, IOcrProvider ocrProvider)
    {
        _parsers = parsers;
        _ocrProvider = ocrProvider;
    }

    public async Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        foreach (var parser in _parsers)
        {
            if (!parser.CanParse(command))
            {
                continue;
            }

            var parsedText = await parser.ParseAsync(command, ct);
            if (!string.IsNullOrWhiteSpace(parsedText))
            {
                return new DocumentTextExtractionResultDto
                {
                    Text = parsedText,
                    Strategy = "direct"
                };
            }
        }

        var ocrResult = await _ocrProvider.ExtractAsync(command.Content, command.FileName, ct);
        return new DocumentTextExtractionResultDto
        {
            Text = ocrResult.ExtractedText,
            Strategy = "ocr",
            Provider = ocrResult.Provider ?? _ocrProvider.ProviderName
        };
    }
}