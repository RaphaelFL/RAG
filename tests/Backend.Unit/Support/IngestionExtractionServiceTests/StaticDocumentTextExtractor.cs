using Chatbot.Application.Abstractions;

namespace Backend.Unit.IngestionExtractionServiceTestsSupport;

internal sealed class StaticDocumentTextExtractor : IDocumentTextExtractor
{
    private readonly string _text;

    public StaticDocumentTextExtractor(string text)
    {
        _text = text;
    }

    public Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        return Task.FromResult(new DocumentTextExtractionResultDto
        {
            Text = _text
        });
    }
}