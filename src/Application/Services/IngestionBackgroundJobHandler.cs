using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Polly;

namespace Chatbot.Application.Services;

public sealed class IngestionBackgroundJobHandler : IIngestionBackgroundJobHandler
{
    private readonly IIngestionCommandFactory _ingestionCommandFactory;
    private readonly IIngestionExtractionService _ingestionExtractionService;
    private readonly IIngestionChunkEnricher _ingestionChunkEnricher;
    private readonly IIngestionDocumentStateService _ingestionDocumentStateService;
    private readonly ISearchIndexGateway _indexGateway;
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<IngestionBackgroundJobHandler> _logger;

    public IngestionBackgroundJobHandler(
        IIngestionCommandFactory ingestionCommandFactory,
        IIngestionExtractionService ingestionExtractionService,
        IIngestionChunkEnricher ingestionChunkEnricher,
        IIngestionDocumentStateService ingestionDocumentStateService,
        ISearchIndexGateway indexGateway,
        IChunkingStrategy chunkingStrategy,
        ResiliencePipeline resiliencePipeline,
        ILogger<IngestionBackgroundJobHandler> logger)
    {
        _ingestionCommandFactory = ingestionCommandFactory;
        _ingestionExtractionService = ingestionExtractionService;
        _ingestionChunkEnricher = ingestionChunkEnricher;
        _ingestionDocumentStateService = ingestionDocumentStateService;
        _indexGateway = indexGateway;
        _chunkingStrategy = chunkingStrategy;
        _resiliencePipeline = resiliencePipeline;
        _logger = logger;
    }

    public async Task ProcessAsync(IngestionBackgroundJob job, CancellationToken ct)
    {
        try
        {
            using var activity = ChatbotTelemetry.ActivitySource.StartActivity("ingestion.process");
            activity?.SetTag("document.id", job.DocumentId);
            var startedAt = DateTime.UtcNow;
            var command = _ingestionCommandFactory.Create(job);
            _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Parsing, job.JobId);

            var extracted = await _ingestionExtractionService.ExtractAsync(job.DocumentId, command, ct);
            if (string.Equals(extracted.Strategy, "ocr", StringComparison.OrdinalIgnoreCase))
            {
                _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.OcrProcessing, job.JobId);
            }

            if (IsFallbackOnlyExtraction(extracted, command.FileName))
            {
                _logger.LogWarning(
                    "Document {documentId} extraction returned only fallback placeholder for {fileName}; aborting indexing",
                    job.DocumentId,
                    command.FileName);
                _ingestionDocumentStateService.MarkIngestionFailure(job.DocumentId, job.JobId);
                return;
            }

            _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Chunking, job.JobId);
            var chunks = _chunkingStrategy.Chunk(command, extracted);
            _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Embedding, job.JobId);
            await _ingestionChunkEnricher.EnrichAsync(chunks, null, false, ct);

            _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Indexing, job.JobId);
            await _resiliencePipeline.ExecuteAsync(async token =>
            {
                await _indexGateway.DeleteDocumentAsync(job.DocumentId, token);
                await _indexGateway.IndexDocumentChunksAsync(chunks, token);
            }, ct);

            _ingestionDocumentStateService.CompleteIngestion(job, chunks.Count);
            ChatbotTelemetry.IngestionLatencyMs.Record((DateTime.UtcNow - startedAt).TotalMilliseconds);

            _logger.LogInformation("Document {documentId} indexed with {chunkCount} chunks", job.DocumentId, chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting document {documentId}", job.DocumentId);
            _ingestionDocumentStateService.MarkIngestionFailure(job.DocumentId, job.JobId);
        }
    }

    private static bool IsFallbackOnlyExtraction(DocumentTextExtractionResultDto extracted, string fileName)
    {
        var fallbackText = $"Conteudo indisponivel para {fileName}";
        if (!string.Equals(extracted.Text.Trim(), fallbackText, StringComparison.Ordinal))
        {
            return false;
        }

        return extracted.Pages.Count == 0 || extracted.Pages.All(page =>
            string.IsNullOrWhiteSpace(page.Text) ||
            string.Equals(page.Text.Trim(), fallbackText, StringComparison.Ordinal));
    }
}