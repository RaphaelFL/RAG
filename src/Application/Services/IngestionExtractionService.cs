using Chatbot.Application.Observability;
using Polly;

namespace Chatbot.Application.Services;

public sealed class IngestionExtractionService : IIngestionExtractionService
{
    private readonly IDocumentTextExtractor _documentTextExtractor;
    private readonly IPromptInjectionDetector _promptInjectionDetector;
    private readonly ISecurityAuditLogger _securityAuditLogger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public IngestionExtractionService(
        IDocumentTextExtractor documentTextExtractor,
        IPromptInjectionDetector promptInjectionDetector,
        ISecurityAuditLogger securityAuditLogger,
        ResiliencePipeline resiliencePipeline)
    {
        _documentTextExtractor = documentTextExtractor;
        _promptInjectionDetector = promptInjectionDetector;
        _securityAuditLogger = securityAuditLogger;
        _resiliencePipeline = resiliencePipeline;
    }

    public async Task<DocumentTextExtractionResultDto> ExtractAsync(Guid documentId, IngestDocumentCommand command, CancellationToken ct)
    {
        var extracted = await _resiliencePipeline.ExecuteAsync(async token =>
            await _documentTextExtractor.ExtractAsync(command, token), ct);
        var normalized = EnsureExtractionHasContent(extracted, command.FileName);

        if (_promptInjectionDetector.TryDetect(normalized.Text, out var pattern))
        {
            _securityAuditLogger.LogPromptInjectionDetected($"document:{documentId}", $"Matched blocked pattern '{pattern}'.");
            ChatbotTelemetry.PromptInjectionSignals.Add(1);
        }

        return normalized;
    }

    private static DocumentTextExtractionResultDto EnsureExtractionHasContent(DocumentTextExtractionResultDto extracted, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(extracted.Text))
        {
            return extracted;
        }

        var fallbackText = $"Conteudo indisponivel para {fileName}";
        return new DocumentTextExtractionResultDto
        {
            Text = fallbackText,
            Strategy = extracted.Strategy,
            Provider = extracted.Provider,
            Pages = new List<PageExtractionDto>
            {
                new()
                {
                    PageNumber = 1,
                    Text = fallbackText
                }
            }
        };
    }
}