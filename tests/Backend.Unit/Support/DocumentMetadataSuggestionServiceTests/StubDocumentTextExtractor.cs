using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit.DocumentMetadataSuggestionServiceTestsSupport;

internal sealed class StubDocumentTextExtractor : IDocumentTextExtractor
{
    private readonly string _text;

    public StubDocumentTextExtractor(string text)
    {
        _text = text;
    }

    public Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        return Task.FromResult(new DocumentTextExtractionResultDto
        {
            Text = _text,
            Strategy = "direct"
        });
    }
}
