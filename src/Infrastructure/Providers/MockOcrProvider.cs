using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Observability;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Providers;

public sealed class MockOcrProvider : IOcrProvider
{
    private readonly OcrOptions _options;
    private readonly ISecurityAuditLogger _securityAuditLogger;

    public MockOcrProvider(IOptions<OcrOptions> options, ISecurityAuditLogger securityAuditLogger)
    {
        _options = options.Value;
        _securityAuditLogger = securityAuditLogger;
    }

    public string ProviderName => _options.PrimaryProvider;

    public async Task<OcrResultDto> ExtractAsync(Stream content, string fileName, CancellationToken ct)
    {
        var selectedProvider = ProviderName;

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var reader = new StreamReader(content, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true);
        var extractedText = await reader.ReadToEndAsync(ct);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        if (string.IsNullOrWhiteSpace(extractedText) && _options.EnableFallback)
        {
            _securityAuditLogger.LogProviderFallback(_options.PrimaryProvider, _options.FallbackProvider, "Empty OCR extraction result.");
            extractedText = $"Fallback extracted text from {fileName}";
            selectedProvider = _options.FallbackProvider;
        }

        extractedText = string.IsNullOrWhiteSpace(extractedText)
            ? $"Sample extracted text from {fileName}"
            : extractedText;

        return new OcrResultDto
        {
            ExtractedText = extractedText,
            Provider = selectedProvider,
            Pages = new List<PageExtractionDto>
            {
                new()
                {
                    PageNumber = 1,
                    Text = extractedText
                }
            }
        };
    }
}
