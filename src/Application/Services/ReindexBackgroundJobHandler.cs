using Chatbot.Application.Abstractions;
using Chatbot.Application.Observability;
using Polly;

namespace Chatbot.Application.Services;

public sealed class ReindexBackgroundJobHandler : IReindexBackgroundJobHandler
{
    private readonly IFullReindexProcessor _fullReindexProcessor;
    private readonly IIncrementalReindexProcessor _incrementalReindexProcessor;
    private readonly IIngestionDocumentStateService _ingestionDocumentStateService;
    private readonly ILogger<ReindexBackgroundJobHandler> _logger;

    public ReindexBackgroundJobHandler(
        IFullReindexProcessor fullReindexProcessor,
        IIncrementalReindexProcessor incrementalReindexProcessor,
        IIngestionDocumentStateService ingestionDocumentStateService,
        ILogger<ReindexBackgroundJobHandler> logger)
    {
        _fullReindexProcessor = fullReindexProcessor;
        _incrementalReindexProcessor = incrementalReindexProcessor;
        _ingestionDocumentStateService = ingestionDocumentStateService;
        _logger = logger;
    }

    public ReindexBackgroundJobHandler(
        IIngestionCommandFactory ingestionCommandFactory,
        IIngestionExtractionService ingestionExtractionService,
        IIngestionChunkEnricher ingestionChunkEnricher,
        IIngestionDocumentStateService ingestionDocumentStateService,
        ISearchIndexGateway indexGateway,
        IChunkingStrategy chunkingStrategy,
        IDocumentCatalog documentCatalog,
        IBlobStorageGateway blobGateway,
        ResiliencePipeline resiliencePipeline,
        ILogger<ReindexBackgroundJobHandler> logger)
    {
        var fullReindexProcessor = new FullReindexProcessor(
            ingestionCommandFactory,
            ingestionExtractionService,
            ingestionChunkEnricher,
            ingestionDocumentStateService,
            indexGateway,
            chunkingStrategy,
            documentCatalog,
            blobGateway,
            resiliencePipeline);

        _fullReindexProcessor = fullReindexProcessor;
        _incrementalReindexProcessor = new IncrementalReindexProcessor(
            ingestionChunkEnricher,
            ingestionDocumentStateService,
            indexGateway,
            documentCatalog,
            fullReindexProcessor,
            resiliencePipeline);
        _ingestionDocumentStateService = ingestionDocumentStateService;
        _logger = logger;
    }

    public async Task ProcessAsync(ReindexBackgroundJob job, CancellationToken ct)
    {
        try
        {
            using var activity = ChatbotTelemetry.ActivitySource.StartActivity("reindex.process");
            activity?.SetTag("document.id", job.DocumentId);
            if (job.FullReindex)
            {
                await _fullReindexProcessor.ProcessAsync(job, ct);
                return;
            }

            await _incrementalReindexProcessor.ProcessAsync(job, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reindexing document {documentId}", job.DocumentId);
            _ingestionDocumentStateService.MarkReindexFailure(job.DocumentId);
        }
    }
}