using Chatbot.Application.Abstractions;

namespace Backend.Unit.IngestionExtractionServiceTestsSupport;

internal sealed class EmptyDocumentTextExtractor : IDocumentTextExtractor
{
    public Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        return Task.FromResult(new DocumentTextExtractionResultDto());
    }
}