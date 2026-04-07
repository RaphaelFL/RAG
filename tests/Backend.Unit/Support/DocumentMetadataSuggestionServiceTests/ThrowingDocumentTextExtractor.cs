using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit.DocumentMetadataSuggestionServiceTestsSupport;

internal sealed class ThrowingDocumentTextExtractor : IDocumentTextExtractor
{
    private readonly Exception _exception;

    public ThrowingDocumentTextExtractor(Exception exception)
    {
        _exception = exception;
    }

    public Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        throw _exception;
    }
}
