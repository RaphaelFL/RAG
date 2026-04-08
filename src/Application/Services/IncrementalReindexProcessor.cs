using Polly;

namespace Chatbot.Application.Services;

public sealed class IncrementalReindexProcessor : IIncrementalReindexProcessor
{
    private readonly IIngestionChunkEnricher _ingestionChunkEnricher;
    private readonly IIngestionDocumentStateService _ingestionDocumentStateService;
    private readonly ISearchIndexGateway _indexGateway;
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IFullReindexProcessor _fullReindexProcessor;
    private readonly ResiliencePipeline _resiliencePipeline;

    public IncrementalReindexProcessor(
        IIngestionChunkEnricher ingestionChunkEnricher,
        IIngestionDocumentStateService ingestionDocumentStateService,
        ISearchIndexGateway indexGateway,
        IDocumentCatalog documentCatalog,
        IFullReindexProcessor fullReindexProcessor,
        ResiliencePipeline resiliencePipeline)
    {
        _ingestionChunkEnricher = ingestionChunkEnricher;
        _ingestionDocumentStateService = ingestionDocumentStateService;
        _indexGateway = indexGateway;
        _documentCatalog = documentCatalog;
        _fullReindexProcessor = fullReindexProcessor;
        _resiliencePipeline = resiliencePipeline;
    }

    public async Task ProcessAsync(ReindexBackgroundJob job, CancellationToken ct)
    {
        await Task.Delay(75, ct);
        var document = _documentCatalog.Get(job.DocumentId);
        if (document is null)
        {
            return;
        }

        var indexedChunks = await _indexGateway.GetDocumentChunksAsync(job.DocumentId, ct);
        if (indexedChunks.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(document.StoragePath))
            {
                await _fullReindexProcessor.ProcessAsync(job, ct);
                return;
            }

            throw new InvalidOperationException($"No persisted chunks were found for document {job.DocumentId}.");
        }

        _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Embedding, job.JobId);
        var updatedEmbeddings = await _ingestionChunkEnricher.EnrichAsync(indexedChunks, job.ForceEmbeddingModel, false, ct);

        if (updatedEmbeddings > 0)
        {
            _ingestionDocumentStateService.UpdateStatus(job.DocumentId, DocumentStatuses.Indexing, job.JobId);
            await _resiliencePipeline.ExecuteAsync(async token =>
                await _indexGateway.IndexDocumentChunksAsync(indexedChunks, token), ct);
        }

        _ingestionDocumentStateService.CompleteReindex(document, job.JobId, indexedChunks.Count);
    }
}