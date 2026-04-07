namespace Chatbot.Application.Services;

public sealed class DocumentMetadataExtractionService : IDocumentMetadataExtractionService
{
    private readonly IDocumentTextExtractor _documentTextExtractor;
    private readonly ILogger<DocumentMetadataExtractionService> _logger;

    public DocumentMetadataExtractionService(IDocumentTextExtractor documentTextExtractor, ILogger<DocumentMetadataExtractionService> logger)
    {
        _documentTextExtractor = documentTextExtractor;
        _logger = logger;
    }

    public async Task<DocumentTextExtractionResultDto> ExtractAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        try
        {
            return await _documentTextExtractor.ExtractAsync(command, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsPdfDocument(command))
        {
            _logger.LogWarning(ex, "Falha ao extrair texto de PDF para sugestao de metadata. Aplicando fallback por nome do arquivo: {fileName}", command.FileName);
            return new DocumentTextExtractionResultDto
            {
                Text = string.Empty,
                Strategy = "filename-fallback"
            };
        }
    }

    private static bool IsPdfDocument(IngestDocumentCommand command)
    {
        return string.Equals(Path.GetExtension(command.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
    }
}