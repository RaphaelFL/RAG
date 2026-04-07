using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Ingestion.Parsers;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Observability;
using Chatbot.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Unit.DocumentTextExtractorTestsSupport;

internal sealed class TrackingOcrProvider : IOcrProvider
{
    public int CallCount { get; private set; }
    public string Text { get; set; } = "ocr";

    public string ProviderName => "Tracking";

    public Task<OcrResultDto> ExtractAsync(Stream content, string fileName, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(new OcrResultDto { ExtractedText = Text });
    }
}
